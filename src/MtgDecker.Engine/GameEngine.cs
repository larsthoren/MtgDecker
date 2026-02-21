using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Actions;

namespace MtgDecker.Engine;

public class GameEngine
{
    private readonly GameState _state;
    private readonly TurnStateMachine _turnStateMachine = new();
    private readonly Dictionary<ActionType, IActionHandler> _handlers = new();

    public GameEngine(GameState state)
    {
        _state = state;
        _handlers[ActionType.UntapCard] = new UntapCardHandler();
        _handlers[ActionType.Cycle] = new CycleHandler();
        _handlers[ActionType.ActivateFetch] = new ActivateFetchHandler();
        _handlers[ActionType.ActivateLoyaltyAbility] = new ActivateLoyaltyAbilityHandler();
        _handlers[ActionType.PlayCard] = new PlayCardHandler();
        _handlers[ActionType.CastAdventure] = new CastAdventureHandler();
        _handlers[ActionType.Ninjutsu] = new NinjutsuHandler();
        _handlers[ActionType.Flashback] = new FlashbackHandler();
        _handlers[ActionType.TapCard] = new TapCardHandler();
        _handlers[ActionType.CastSpell] = new CastSpellHandler();
    }

    public async Task StartGameAsync(CancellationToken ct = default)
    {
        _state.Player1.Library.Shuffle();
        _state.Player2.Library.Shuffle();

        DrawCards(_state.Player1, 7);
        DrawCards(_state.Player2, 7);

        await RunMulliganAsync(_state.Player1, ct);
        await RunMulliganAsync(_state.Player2, ct);

        _state.Log("Game started.");
    }

    public async Task RunTurnAsync(CancellationToken ct = default)
    {
        _turnStateMachine.Reset();
        _state.ActivePlayer.LandsPlayedThisTurn = 0;
        _state.Player1.CreaturesDiedThisTurn = 0;
        _state.Player2.CreaturesDiedThisTurn = 0;
        _state.Player1.DrawsThisTurn = 0;
        _state.Player2.DrawsThisTurn = 0;
        RemoveExpiredEffects();
        _state.Player1.DrawStepDrawExempted = false;
        _state.Player2.DrawStepDrawExempted = false;
        _state.Player1.PlaneswalkerAbilitiesUsedThisTurn.Clear();
        _state.Player2.PlaneswalkerAbilitiesUsedThisTurn.Clear();
        _state.Player1.LifeLostThisTurn = 0;
        _state.Player2.LifeLostThisTurn = 0;
        _state.Player1.PermanentLeftBattlefieldThisTurn = false;
        _state.Player2.PermanentLeftBattlefieldThisTurn = false;
        _state.Log($"Turn {_state.TurnNumber}: {_state.ActivePlayer.Name}'s turn.");

        do
        {
            var phase = _turnStateMachine.CurrentPhase;
            _state.CurrentPhase = phase.Phase;
            _state.Log($"Phase: {phase.Phase}");

            if (phase.HasTurnBasedAction)
            {
                bool skipDraw = phase.Phase == Phase.Draw && _state.IsFirstTurn;
                if (!skipDraw)
                    ExecuteTurnBasedAction(phase.Phase);
            }

            if (_state.IsGameOver) return;

            // Fire upkeep triggers (e.g., Mirri's Guile, Sylvan Library)
            if (phase.Phase == Phase.Upkeep)
            {
                await QueueBoardTriggersOnStackAsync(GameEvent.Upkeep, null, ct);
                await QueueGraveyardTriggersOnStackAsync(GameEvent.Upkeep, ct);
                await QueueEchoTriggersOnStackAsync(ct);
            }

            if (phase.Phase == Phase.Combat)
            {
                await RunCombatAsync(ct);
            }
            else if (phase.GrantsPriority)
            {
                await RunPriorityAsync(ct);
            }

            if (_state.IsGameOver) return;

            _state.Player1.ManaPool.Clear();
            _state.Player2.ManaPool.Clear();
            _state.Player1.PendingManaTaps.Clear();
            _state.Player2.PendingManaTaps.Clear();

        } while (_turnStateMachine.AdvancePhase() != null);

        // Process delayed triggers at end step (e.g., Goblin Pyromancer destroys all Goblins)
        await QueueDelayedTriggersOnStackAsync(GameEvent.EndStep, ct);
        if (_state.StackCount > 0)
            await ResolveAllTriggersAsync(ct);

        // Cleanup: discard to maximum hand size (MTG rule 514.1)
        await DiscardToHandSizeAsync(_state.ActivePlayer, ct);

        // Clear end-of-turn effects and recalculate
        StripEndOfTurnEffects();
        RecalculateState();

        // Clear damage at end of turn
        ClearDamage();

        _state.IsFirstTurn = false;
        _state.TurnNumber++;

        if (_state.ExtraTurns.Count > 0)
        {
            var extraPlayerId = _state.ExtraTurns.Dequeue();
            _state.ActivePlayer = _state.GetPlayer(extraPlayerId);
            _state.Log($"Extra turn for {_state.ActivePlayer.Name}!");
        }
        else
        {
            _state.ActivePlayer = _state.GetOpponent(_state.ActivePlayer);
        }
    }

    private async Task DiscardToHandSizeAsync(Player player, CancellationToken ct)
    {
        const int maxHandSize = 7;
        var excess = player.Hand.Cards.Count - maxHandSize;
        if (excess <= 0) return;

        _state.Log($"{player.Name} must discard {excess} card(s) to hand size.");

        var chosen = await player.DecisionHandler.ChooseCardsToDiscard(
            player.Hand.Cards, excess, ct);

        foreach (var card in chosen.Take(excess))
        {
            player.Hand.Remove(card);
            MoveToGraveyardWithReplacement(card, player);
            _state.Log($"{player.Name} discards {card.Name}.");
        }
    }

    internal void ExecuteTurnBasedAction(Phase phase)
    {
        switch (phase)
        {
            case Phase.Untap:
                foreach (var card in _state.ActivePlayer.Battlefield.Cards)
                {
                    if (card.GetCounters(CounterType.Stun) > 0)
                    {
                        card.RemoveCounter(CounterType.Stun);
                        _state.Log($"Removed a stun counter from {card.Name} (instead of untapping).");
                    }
                    else if (card.IsTapped)
                    {
                        card.IsTapped = false;
                    }
                }
                _state.ActivePlayer.PendingManaTaps.Clear();
                _state.Log($"{_state.ActivePlayer.Name} untaps all permanents.");
                break;

            case Phase.Draw:
                // Check for SkipDraw effects on the active player's permanents
                var hasSkipDraw = _state.ActiveEffects.Any(e =>
                    e.Type == ContinuousEffectType.SkipDraw
                    && (_state.Player1.Battlefield.Contains(e.SourceId)
                        ? _state.Player1 : _state.Player2).Id == _state.ActivePlayer.Id);

                if (hasSkipDraw)
                {
                    _state.Log($"{_state.ActivePlayer.Name}'s draw is skipped.");
                    break;
                }

                _state.ActivePlayer.DrawStepDrawExempted = false;
                DrawCards(_state.ActivePlayer, 1, isDrawStepDraw: true);
                if (!_state.IsGameOver)
                    _state.Log($"{_state.ActivePlayer.Name} draws a card.");
                break;
        }
    }

    internal async Task ExecuteAction(GameAction action, CancellationToken ct = default)
    {
        if (action.PlayerId != _state.Player1.Id && action.PlayerId != _state.Player2.Id)
            throw new InvalidOperationException($"Unknown player ID: {action.PlayerId}");

        // Dispatch to extracted handler if available
        if (_handlers.TryGetValue(action.Type, out var handler))
        {
            await handler.ExecuteAsync(action, this, _state, ct);
            return;
        }

        var player = _state.GetPlayer(action.PlayerId);

        switch (action.Type)
        {
            case ActionType.ActivateAbility:
            {
                var abilitySource = player.Battlefield.Cards.FirstOrDefault(c => c.Id == action.CardId);
                if (abilitySource == null) break;

                // Suppression: creatures that lost abilities can't activate abilities
                if (abilitySource.IsCreature && abilitySource.AbilitiesRemoved)
                {
                    _state.Log($"{abilitySource.Name} has lost its abilities — cannot activate.");
                    break;
                }

                // Look up activated ability: token ability on the card, then CardDefinitions registry
                ActivatedAbility? ability = abilitySource.TokenActivatedAbility;
                if (ability == null)
                {
                    if (CardDefinitions.TryGet(abilitySource.Name, out var abilityDef))
                        ability = abilityDef.ActivatedAbility;
                }

                if (ability == null)
                {
                    _state.Log($"{abilitySource.Name} has no activated ability.");
                    break;
                }
                var cost = ability.Cost;

                // Validate: activation condition (e.g., threshold)
                if (ability.Condition != null && !ability.Condition(player))
                {
                    _state.Log($"Cannot activate {abilitySource.Name} — condition not met.");
                    break;
                }

                // Validate: tap cost when already tapped
                if (cost.TapSelf && abilitySource.IsTapped)
                {
                    _state.Log($"Cannot activate {abilitySource.Name} — already tapped.");
                    break;
                }

                // Validate: summoning sickness prevents creatures from using tap abilities
                if (cost.TapSelf && abilitySource.IsCreature && abilitySource.HasSummoningSickness(_state.TurnNumber))
                {
                    _state.Log($"{abilitySource.Name} has summoning sickness.");
                    break;
                }

                // Validate: pay life cost
                if (cost.PayLife > 0 && player.Life < cost.PayLife)
                {
                    _state.Log($"Cannot activate {abilitySource.Name} — not enough life (need {cost.PayLife}, have {player.Life}).");
                    break;
                }

                // Validate: mana cost
                if (cost.ManaCost != null && !player.ManaPool.CanPay(cost.ManaCost))
                {
                    _state.Log($"Cannot activate {abilitySource.Name} — not enough mana.");
                    break;
                }

                // Validate: counter removal cost
                if (cost.RemoveCounterType.HasValue)
                {
                    if (abilitySource.GetCounters(cost.RemoveCounterType.Value) <= 0)
                    {
                        _state.Log($"Cannot activate {abilitySource.Name} — no {cost.RemoveCounterType.Value} counters.");
                        break;
                    }
                }

                // Validate: sacrifice subtype
                GameCard? sacrificeTarget = null;
                if (cost.SacrificeSubtype != null)
                {
                    var eligible = player.Battlefield.Cards
                        .Where(c => c.IsCreature && c.Subtypes.Contains(cost.SacrificeSubtype, StringComparer.OrdinalIgnoreCase))
                        .ToList();

                    if (eligible.Count == 0)
                    {
                        _state.Log($"Cannot activate {abilitySource.Name} — no eligible {cost.SacrificeSubtype} to sacrifice.");
                        break;
                    }

                    var chosenId = await player.DecisionHandler.ChooseCard(
                        eligible, $"Choose a {cost.SacrificeSubtype} to sacrifice", optional: false, ct);

                    if (chosenId.HasValue)
                        sacrificeTarget = eligible.FirstOrDefault(c => c.Id == chosenId.Value);

                    if (sacrificeTarget == null)
                    {
                        _state.Log($"Cannot activate {abilitySource.Name} — no sacrifice target chosen.");
                        break;
                    }
                }

                // Validate: sacrifice card type
                GameCard? sacrificeByType = null;
                if (cost.SacrificeCardType.HasValue)
                {
                    var eligible = player.Battlefield.Cards
                        .Where(c => c.CardTypes.HasFlag(cost.SacrificeCardType.Value))
                        .ToList();

                    if (eligible.Count == 0)
                    {
                        _state.Log($"Cannot activate {abilitySource.Name} — no {cost.SacrificeCardType.Value} to sacrifice.");
                        break;
                    }

                    var chosenId = await player.DecisionHandler.ChooseCard(
                        eligible, $"Choose a {cost.SacrificeCardType.Value} to sacrifice", optional: false, ct);

                    if (chosenId.HasValue)
                        sacrificeByType = eligible.FirstOrDefault(c => c.Id == chosenId.Value);

                    if (sacrificeByType == null)
                    {
                        _state.Log($"Cannot activate {abilitySource.Name} — no sacrifice target chosen.");
                        break;
                    }
                }

                // Validate: discard card type from hand
                GameCard? discardTarget = null;
                if (cost.DiscardCardType.HasValue)
                {
                    var eligible = player.Hand.Cards
                        .Where(c => c.CardTypes.HasFlag(cost.DiscardCardType.Value))
                        .ToList();

                    if (eligible.Count == 0)
                    {
                        _state.Log($"Cannot activate {abilitySource.Name} — no {cost.DiscardCardType.Value} in hand to discard.");
                        break;
                    }

                    var chosenId = await player.DecisionHandler.ChooseCard(
                        eligible, $"Choose a {cost.DiscardCardType.Value} to discard", optional: false, ct);

                    if (chosenId.HasValue)
                        discardTarget = eligible.FirstOrDefault(c => c.Id == chosenId.Value);

                    if (discardTarget == null)
                    {
                        _state.Log($"Cannot activate {abilitySource.Name} — no discard target chosen.");
                        break;
                    }
                }

                // Pay costs: mana
                if (cost.ManaCost != null)
                {
                    await PayManaCostAsync(cost.ManaCost, player, ct);
                    player.PendingManaTaps.Clear();
                }

                // Pay costs: tap self
                if (cost.TapSelf)
                    abilitySource.IsTapped = true;

                // Pay costs: sacrifice self
                if (cost.SacrificeSelf)
                {
                    await FireLeaveBattlefieldTriggersAsync(abilitySource, player, ct);
                    player.Battlefield.RemoveById(abilitySource.Id);
                    player.Graveyard.Add(abilitySource);
                    _state.Log($"{player.Name} sacrifices {abilitySource.Name}.");
                }

                // Pay costs: sacrifice subtype target
                if (sacrificeTarget != null)
                {
                    await FireLeaveBattlefieldTriggersAsync(sacrificeTarget, player, ct);
                    player.Battlefield.RemoveById(sacrificeTarget.Id);
                    player.Graveyard.Add(sacrificeTarget);
                    _state.Log($"{player.Name} sacrifices {sacrificeTarget.Name}.");
                }

                // Pay costs: sacrifice card type target
                if (sacrificeByType != null)
                {
                    await FireLeaveBattlefieldTriggersAsync(sacrificeByType, player, ct);
                    player.Battlefield.RemoveById(sacrificeByType.Id);
                    player.Graveyard.Add(sacrificeByType);
                    _state.Log($"{player.Name} sacrifices {sacrificeByType.Name}.");
                }

                // Pay costs: remove counter
                if (cost.RemoveCounterType.HasValue)
                {
                    abilitySource.RemoveCounter(cost.RemoveCounterType.Value);
                }

                // Pay costs: discard card type
                if (discardTarget != null)
                {
                    player.Hand.RemoveById(discardTarget.Id);
                    player.Graveyard.Add(discardTarget);
                    _state.Log($"{player.Name} discards {discardTarget.Name}.");
                }

                // Pay costs: life
                if (cost.PayLife > 0)
                {
                    player.AdjustLife(-cost.PayLife);
                    _state.Log($"{player.Name} pays {cost.PayLife} life.");
                }

                // Find or prompt for effect target
                GameCard? effectTarget = null;
                if (action.TargetCardId.HasValue)
                {
                    effectTarget = _state.Player1.Battlefield.Cards.FirstOrDefault(c => c.Id == action.TargetCardId.Value)
                                ?? _state.Player2.Battlefield.Cards.FirstOrDefault(c => c.Id == action.TargetCardId.Value);
                }
                else if (ability.TargetFilter != null && !action.TargetPlayerId.HasValue)
                {
                    // No target was pre-selected — prompt for one
                    var opponent = _state.GetOpponent(player);
                    var eligible = player.Battlefield.Cards
                        .Concat(opponent.Battlefield.Cards)
                        .Where(c => ability.TargetFilter(c) && !CannotBeTargetedBy(c, player))
                        .ToList();

                    if (eligible.Count == 0)
                    {
                        _state.Log($"No legal targets for {abilitySource.Name}.");
                        break;
                    }

                    var target = await player.DecisionHandler.ChooseTarget(
                        abilitySource.Name, eligible, opponent.Id, ct);

                    if (target == null)
                    {
                        _state.Log($"{player.Name} cancels activating {abilitySource.Name}.");
                        break;
                    }

                    effectTarget = eligible.FirstOrDefault(c => c.Id == target.CardId);
                }

                // Shroud/Hexproof check: cannot target a permanent with shroud or hexproof (opponent only)
                if (effectTarget != null && CannotBeTargetedBy(effectTarget, player))
                {
                    var reason = HasShroud(effectTarget) ? "shroud" : "hexproof";
                    _state.Log($"{effectTarget.Name} has {reason} — cannot be targeted.");
                    break;
                }

                // Player shroud check: cannot target a player with shroud
                if (action.TargetPlayerId.HasValue && HasPlayerShroud(action.TargetPlayerId.Value))
                {
                    var targetPlayerName = action.TargetPlayerId.Value == _state.Player1.Id
                        ? _state.Player1.Name : _state.Player2.Name;
                    _state.Log($"{targetPlayerName} has shroud — cannot be targeted.");
                    break;
                }

                // Push activated ability onto the stack (MTG rules: abilities use the stack)
                var stackObj = new TriggeredAbilityStackObject(abilitySource, player.Id, ability.Effect, effectTarget)
                {
                    TargetPlayerId = action.TargetPlayerId,
                };
                _state.StackPush(stackObj);
                _state.Log($"{abilitySource.Name}'s ability is put on the stack.");

                player.ActionHistory.Push(action);
                break;
            }

        }
    }

    /// <summary>
    /// Pays a mana cost from the player's mana pool, prompting for generic payment choices when ambiguous.
    /// Returns a dictionary of colors actually paid (for undo tracking).
    /// Assumes CanPay has already been checked.
    /// </summary>
    internal async Task<Dictionary<ManaColor, int>> PayManaCostAsync(ManaCost cost, Player player, CancellationToken ct)
    {
        var pool = player.ManaPool;
        var manaPaid = new Dictionary<ManaColor, int>();

        // Calculate remaining pool after colored requirements (for generic payment decisions)
        var remaining = new Dictionary<ManaColor, int>();
        foreach (var kvp in pool.Available)
        {
            var after = kvp.Value;
            if (cost.ColorRequirements.TryGetValue(kvp.Key, out var needed))
                after -= needed;
            if (after > 0)
                remaining[kvp.Key] = after;
        }

        // Deduct colored requirements
        foreach (var (color, required) in cost.ColorRequirements)
        {
            pool.Deduct(color, required);
            manaPaid[color] = required;
        }

        // Handle generic cost
        if (cost.GenericCost > 0)
        {
            int distinctColors = remaining.Count(kv => kv.Value > 0);
            int totalRemaining = remaining.Values.Sum();
            bool useAutoPay = distinctColors <= 1 || totalRemaining == cost.GenericCost;

            if (!useAutoPay)
            {
                // Ambiguous: prompt player for generic payment choices
                var genericPayment = await player.DecisionHandler
                    .ChooseGenericPayment(cost.GenericCost, remaining, ct);

                // Validate payment: sum must equal generic cost, amounts must not exceed available
                bool valid = genericPayment.Values.Sum() == cost.GenericCost
                    && genericPayment.All(kv => remaining.TryGetValue(kv.Key, out var avail) && kv.Value <= avail);

                if (valid)
                {
                    foreach (var (color, amount) in genericPayment)
                    {
                        pool.Deduct(color, amount);
                        manaPaid[color] = manaPaid.GetValueOrDefault(color) + amount;
                    }
                }
                else
                {
                    useAutoPay = true;
                }
            }

            if (useAutoPay)
            {
                var toPay = cost.GenericCost;
                foreach (var (color, amount) in remaining)
                {
                    var take = Math.Min(amount, toPay);
                    if (take > 0)
                    {
                        pool.Deduct(color, take);
                        manaPaid[color] = manaPaid.GetValueOrDefault(color) + take;
                        toPay -= take;
                    }
                    if (toPay == 0) break;
                }
            }
        }

        return manaPaid;
    }

    internal bool CanPayAlternateCost(AlternateCost alt, Player player, GameCard castCard)
    {
        // Life check: must have strictly more life than the cost
        if (alt.LifeCost > 0 && player.Life <= alt.LifeCost) return false;

        // Exile a card of the required color from hand (not the spell itself)
        if (alt.ExileCardColor.HasValue)
        {
            var hasColoredCard = player.Hand.Cards.Any(c =>
                c.Id != castCard.Id && c.ManaCost != null &&
                c.ManaCost.ColorRequirements.ContainsKey(alt.ExileCardColor.Value));
            if (!hasColoredCard) return false;
        }

        // Return land with matching subtype from battlefield
        if (alt.ReturnLandSubtype != null)
        {
            var hasLand = player.Battlefield.Cards.Any(c =>
                c.IsLand && c.Subtypes.Contains(alt.ReturnLandSubtype));
            if (!hasLand) return false;
        }

        // Sacrifice lands with matching subtype
        if (alt.SacrificeLandSubtype != null)
        {
            var landCount = player.Battlefield.Cards.Count(c =>
                c.IsLand && c.Subtypes.Contains(alt.SacrificeLandSubtype));
            if (landCount < alt.SacrificeLandCount) return false;
        }

        // Requires controlling a permanent with the given subtype
        if (alt.RequiresControlSubtype != null)
        {
            var hasControlled = player.Battlefield.Cards.Any(c =>
                c.Subtypes.Contains(alt.RequiresControlSubtype));
            if (!hasControlled) return false;
        }

        return true;
    }

    internal async Task PayAlternateCostAsync(AlternateCost alt, Player player, GameCard castCard, CancellationToken ct)
    {
        // Pay life
        if (alt.LifeCost > 0)
        {
            player.AdjustLife(-alt.LifeCost);
            _state.Log($"{player.Name} pays {alt.LifeCost} life.");
        }

        // Exile card from hand
        if (alt.ExileCardColor.HasValue)
        {
            var eligible = player.Hand.Cards.Where(c =>
                c.Id != castCard.Id && c.ManaCost != null &&
                c.ManaCost.ColorRequirements.ContainsKey(alt.ExileCardColor.Value)).ToList();

            var chosenId = await player.DecisionHandler.ChooseCard(
                eligible, $"Choose a {alt.ExileCardColor} card to exile", optional: false, ct);

            if (chosenId.HasValue)
            {
                var exiled = player.Hand.Cards.FirstOrDefault(c => c.Id == chosenId.Value);
                if (exiled != null)
                {
                    player.Hand.RemoveById(exiled.Id);
                    player.Exile.Add(exiled);
                    _state.Log($"{player.Name} exiles {exiled.Name} from hand.");
                }
            }
        }

        // Return land to hand
        if (alt.ReturnLandSubtype != null)
        {
            var eligible = player.Battlefield.Cards.Where(c =>
                c.IsLand && c.Subtypes.Contains(alt.ReturnLandSubtype)).ToList();

            var chosenId = await player.DecisionHandler.ChooseCard(
                eligible, $"Choose an {alt.ReturnLandSubtype} to return", optional: false, ct);

            if (chosenId.HasValue)
            {
                var land = player.Battlefield.Cards.FirstOrDefault(c => c.Id == chosenId.Value);
                if (land != null)
                {
                    player.Battlefield.RemoveById(land.Id);
                    player.Hand.Add(land);
                    _state.Log($"{player.Name} returns {land.Name} to hand.");
                }
            }
        }

        // Sacrifice lands
        if (alt.SacrificeLandSubtype != null)
        {
            for (int i = 0; i < alt.SacrificeLandCount; i++)
            {
                var eligible = player.Battlefield.Cards.Where(c =>
                    c.IsLand && c.Subtypes.Contains(alt.SacrificeLandSubtype)).ToList();

                if (eligible.Count == 0) break;

                var chosenId = await player.DecisionHandler.ChooseCard(
                    eligible, $"Choose a {alt.SacrificeLandSubtype} to sacrifice ({i + 1}/{alt.SacrificeLandCount})", optional: false, ct);

                if (chosenId.HasValue)
                {
                    var land = player.Battlefield.Cards.FirstOrDefault(c => c.Id == chosenId.Value);
                    if (land != null)
                    {
                        await FireLeaveBattlefieldTriggersAsync(land, player, ct);
                        player.Battlefield.RemoveById(land.Id);
                        player.Graveyard.Add(land);
                        _state.Log($"{player.Name} sacrifices {land.Name}.");
                    }
                }
            }
        }
    }

    internal bool HasShroud(GameCard card) => card.ActiveKeywords.Contains(Keyword.Shroud);

    private bool HasHexproof(GameCard card) => card.ActiveKeywords.Contains(Keyword.Hexproof);

    /// <summary>
    /// Shared targeting helper — builds eligible targets, prompts player, validates choice.
    /// Returns null if the player cancels targeting. Returns empty list if no legal targets exist.
    /// </summary>
    internal async Task<List<TargetInfo>?> FindAndChooseTargetsAsync(
        TargetFilter filter, Player caster, IPlayerDecisionHandler handler,
        string? spellName = null, CancellationToken ct = default)
    {
        var opponent = _state.GetOpponent(caster);
        var eligible = new List<GameCard>();

        foreach (var c in caster.Battlefield.Cards)
            if (filter.IsLegal(c, ZoneType.Battlefield) && !CannotBeTargetedBy(c, caster))
                eligible.Add(c);
        foreach (var c in opponent.Battlefield.Cards)
            if (filter.IsLegal(c, ZoneType.Battlefield) && !CannotBeTargetedBy(c, caster))
                eligible.Add(c);

        var dummyCard = new GameCard { Name = "Player" };
        if (filter.IsLegal(dummyCard, ZoneType.None))
        {
            eligible.Add(new GameCard { Id = Guid.Empty, Name = caster.Name });
            eligible.Add(new GameCard { Id = Guid.Empty, Name = opponent.Name });
        }

        if (filter.IsLegal(dummyCard, ZoneType.Stack))
        {
            foreach (var so in _state.Stack.OfType<StackObject>())
                if (filter.IsLegal(so.Card, ZoneType.Stack))
                    eligible.Add(so.Card);
        }

        if (eligible.Count == 0)
            return new List<TargetInfo>();

        var target = await handler.ChooseTarget(
            spellName ?? "spell", eligible, opponent.Id, ct);

        if (target == null)
            return null;

        var targets = new List<TargetInfo>();
        if (target.Zone == ZoneType.None)
        {
            targets.Add(new TargetInfo(Guid.Empty, target.PlayerId, ZoneType.None));
        }
        else
        {
            var stackTarget = _state.Stack.OfType<StackObject>()
                .FirstOrDefault(s => s.Card.Id == target.CardId);
            if (stackTarget != null)
                targets.Add(new TargetInfo(stackTarget.Card.Id, stackTarget.ControllerId, ZoneType.Stack));
            else
                targets.Add(target);
        }

        return targets;
    }

    /// <summary>
    /// Returns true if the card cannot be targeted by the given player.
    /// Shroud prevents all targeting; Hexproof prevents only opponent targeting.
    /// </summary>
    internal bool CannotBeTargetedBy(GameCard card, Player caster)
    {
        if (HasShroud(card)) return true;
        if (HasHexproof(card))
        {
            // Hexproof only blocks opponents — controller can still target
            var controller = GetCardController(card);
            if (controller != null && controller.Id != caster.Id)
                return true;
        }
        return false;
    }

    private Player? GetCardController(GameCard card)
    {
        if (_state.Player1.Battlefield.Contains(card.Id)) return _state.Player1;
        if (_state.Player2.Battlefield.Contains(card.Id)) return _state.Player2;
        return null;
    }

    internal bool HasPlayerShroud(Guid playerId)
    {
        return _state.ActiveEffects.Any(e =>
            e.Type == ContinuousEffectType.GrantPlayerShroud
            && GetEffectController(e.SourceId)?.Id == playerId);
    }

    private bool HasPlayerDamageProtection(Guid playerId)
    {
        return _state.ActiveEffects.Any(e =>
            e.Type == ContinuousEffectType.PreventDamageToPlayer
            && GetEffectController(e.SourceId)?.Id == playerId);
    }

    private Player? GetEffectController(Guid sourceId)
    {
        if (_state.Player1.Battlefield.Contains(sourceId)) return _state.Player1;
        if (_state.Player2.Battlefield.Contains(sourceId)) return _state.Player2;
        return null;
    }

    internal async Task TryAttachAuraAsync(GameCard playCard, Player player, CancellationToken ct)
    {
        if (!CardDefinitions.TryGet(playCard.Name, out var auraDef) || !auraDef.AuraTarget.HasValue)
            return;

        var eligible = player.Battlefield.Cards.Concat(
            _state.GetOpponent(player).Battlefield.Cards)
            .Where(c => c.Id != playCard.Id) // Don't target itself
            .Where(c => auraDef.AuraTarget switch
            {
                AuraTarget.Land => c.IsLand,
                AuraTarget.Creature => c.IsCreature,
                AuraTarget.Permanent => true,
                _ => false,
            })
            .Where(c => !CannotBeTargetedBy(c, player))
            .ToList();

        if (eligible.Count > 0)
        {
            var opponent = _state.GetOpponent(player);
            var target = await player.DecisionHandler.ChooseTarget(
                playCard.Name, eligible, opponent.Id, ct);
            if (target != null)
                playCard.AttachedTo = target.CardId;
        }

        if (!playCard.AttachedTo.HasValue)
        {
            player.Battlefield.RemoveById(playCard.Id);
            player.Graveyard.Add(playCard);
            _state.Log($"{playCard.Name} has no valid target — goes to graveyard.");
        }
    }

    public bool CanCastSorcery(Guid playerId)
    {
        return _state.ActivePlayer.Id == playerId
            && (_state.CurrentPhase == Phase.MainPhase1 || _state.CurrentPhase == Phase.MainPhase2)
            && _state.StackCount == 0;
    }

    public bool UndoLastAction(Guid playerId)
    {
        var player = _state.GetPlayer(playerId);
        if (player.ActionHistory.Count == 0) return false;

        var action = player.ActionHistory.Peek();

        // Only TapCard can be undone, and only if mana is unspent
        if (action.Type != ActionType.TapCard)
        {
            _state.Log("Only land taps with unspent mana can be undone.");
            return false;
        }

        if (!player.PendingManaTaps.Contains(action.CardId!.Value))
        {
            _state.Log("Mana already spent — tap cannot be undone.");
            return false;
        }

        var tapTarget = player.Battlefield.Cards.FirstOrDefault(c => c.Id == action.CardId);
        if (tapTarget == null) return false;

        player.ActionHistory.Pop();
        tapTarget.IsTapped = false;
        player.PendingManaTaps.Remove(tapTarget.Id);
        if (action.ManaProduced.HasValue)
            player.ManaPool.Deduct(action.ManaProduced.Value, action.ManaProducedAmount);
        // Also deduct bonus mana from aura triggers (e.g., Wild Growth) and filter lands
        if (action.BonusManaProduced != null)
        {
            foreach (var bonusColor in action.BonusManaProduced)
                player.ManaPool.Deduct(bonusColor, 1);
        }
        // Restore mana spent on filter land activation cost
        if (action.ActualManaPaid != null)
        {
            foreach (var (color, amount) in action.ActualManaPaid)
                player.ManaPool.Add(color, amount);
        }
        // Restore pain damage from painlands
        if (action.PainDamageDealt)
            player.AdjustLife(1);
        _state.Log($"{player.Name} untaps {tapTarget.Name}.");
        return true;
    }

    public async Task RunCombatAsync(CancellationToken ct)
    {
        var attacker = _state.ActivePlayer;
        var defender = _state.GetOpponent(attacker);

        // Begin Combat
        _state.CombatStep = CombatStep.BeginCombat;
        _state.Combat = new CombatState(attacker.Id, defender.Id);
        _state.Log("Beginning of combat.");

        // Priority at beginning of combat (allows phase stops and instant-speed plays)
        await RunPriorityAsync(ct);

        // Declare Attackers
        _state.CombatStep = CombatStep.DeclareAttackers;

        var eligibleAttackers = attacker.Battlefield.Cards
            .Where(c => c.IsCreature && !c.IsTapped && !c.HasSummoningSickness(_state.TurnNumber)
                && !c.ActiveKeywords.Contains(Keyword.Defender))
            .ToList();

        if (eligibleAttackers.Count == 0)
        {
            _state.Log("No eligible attackers.");
            _state.CombatStep = CombatStep.None;
            _state.Combat = null;
            return;
        }

        var chosenAttackerIds = await attacker.DecisionHandler.ChooseAttackers(eligibleAttackers, ct);

        // Filter to only valid attackers
        var validAttackerIds = chosenAttackerIds
            .Where(id => eligibleAttackers.Any(c => c.Id == id))
            .ToList();

        if (validAttackerIds.Count == 0)
        {
            _state.Log("No attackers declared.");
            _state.CombatStep = CombatStep.None;
            _state.Combat = null;
            return;
        }

        // Tap attackers and register them
        foreach (var attackerId in validAttackerIds)
        {
            var card = attacker.Battlefield.Cards.First(c => c.Id == attackerId);
            card.IsTapped = true;
            _state.Combat.DeclareAttacker(attackerId);
            _state.Log($"{attacker.Name} attacks with {card.Name} ({card.Power}/{card.Toughness}).");
        }

        // Choose attacker targets when defending player controls planeswalkers
        var defenderPlaneswalkers = defender.Battlefield.Cards
            .Where(c => c.IsPlaneswalker)
            .ToList();

        if (defenderPlaneswalkers.Count > 0)
        {
            var declaredAttackerCards = validAttackerIds
                .Select(id => attacker.Battlefield.Cards.First(c => c.Id == id))
                .ToList();

            var targets = await attacker.DecisionHandler.ChooseAttackerTargets(declaredAttackerCards, defenderPlaneswalkers, ct);

            foreach (var (attackerId, targetId) in targets)
            {
                if (validAttackerIds.Contains(attackerId))
                {
                    _state.Combat.SetAttackerTarget(attackerId, targetId);
                    if (targetId.HasValue)
                    {
                        var targetPw = defenderPlaneswalkers.FirstOrDefault(pw => pw.Id == targetId.Value);
                        var attackerCard = attacker.Battlefield.Cards.First(c => c.Id == attackerId);
                        if (targetPw != null)
                            _state.Log($"{attackerCard.Name} targets {targetPw.Name}.");
                    }
                }
            }
        }

        // Fire SelfAttacks triggers (e.g., Piledriver pump) after attackers declared
        foreach (var attackerId in validAttackerIds)
        {
            var card = attacker.Battlefield.Cards.FirstOrDefault(c => c.Id == attackerId);
            if (card != null)
                await QueueAttackTriggersOnStackAsync(card, ct);
        }

        // Priority round after attack triggers
        if (_state.StackCount > 0)
            await RunPriorityAsync(ct);

        // Re-validate attackers — creatures may have been exiled during priority
        validAttackerIds = validAttackerIds
            .Where(id => attacker.Battlefield.Cards.Any(c => c.Id == id))
            .ToList();
        if (validAttackerIds.Count == 0)
        {
            _state.Log("All attackers removed from combat.");
            _state.CombatStep = CombatStep.None;
            _state.Combat = null;
            return;
        }

        // Declare Blockers
        _state.CombatStep = CombatStep.DeclareBlockers;

        var attackerCards = validAttackerIds
            .Select(id => attacker.Battlefield.Cards.First(c => c.Id == id))
            .ToList();

        var eligibleBlockers = defender.Battlefield.Cards
            .Where(c => c.IsCreature && !c.IsTapped)
            .ToList();

        if (eligibleBlockers.Count > 0)
        {
            var blockerAssignments = await defender.DecisionHandler.ChooseBlockers(eligibleBlockers, attackerCards, ct);

            // Validate and register blocker assignments
            foreach (var (blockerId, attackerCardId) in blockerAssignments)
            {
                if (eligibleBlockers.Any(c => c.Id == blockerId) && validAttackerIds.Contains(attackerCardId))
                {
                    var attackerCard = attacker.Battlefield.Cards.FirstOrDefault(c => c.Id == attackerCardId);
                    var blockerCard = defender.Battlefield.Cards.FirstOrDefault(c => c.Id == blockerId);
                    if (attackerCard == null || blockerCard == null) continue;

                    // Mountainwalk: cannot be blocked if defender controls a Mountain
                    if (attackerCard.ActiveKeywords.Contains(Keyword.Mountainwalk)
                        && defender.Battlefield.Cards.Any(c => c.Subtypes.Contains("Mountain")))
                    {
                        _state.Log($"{attackerCard.Name} has mountainwalk — cannot be blocked.");
                        continue;
                    }

                    _state.Combat.DeclareBlocker(blockerId, attackerCardId);
                    _state.Log($"{defender.Name} blocks {attackerCard.Name} with {blockerCard.Name}.");
                }
            }
        }

        // Order blockers for multi-block scenarios
        foreach (var attackerId in validAttackerIds)
        {
            var blockers = _state.Combat.GetBlockers(attackerId);
            if (blockers.Count > 1)
            {
                var blockerCards = blockers
                    .Select(id => defender.Battlefield.Cards.FirstOrDefault(c => c.Id == id))
                    .Where(c => c != null)
                    .ToList();

                if (blockerCards.Count > 1)
                {
                    var orderedIds = await attacker.DecisionHandler.OrderBlockers(attackerId, blockerCards!, ct);
                    _state.Combat.SetBlockerOrder(attackerId, orderedIds.ToList());
                }
            }
        }

        // Combat Damage
        _state.CombatStep = CombatStep.CombatDamage;
        var unblocked = ResolveCombatDamage(attacker, defender);

        // Fire CombatDamageDealt triggers for unblocked attackers that dealt damage to the player
        foreach (var unblockedAttacker in unblocked)
        {
            await QueueBoardTriggersOnStackAsync(GameEvent.CombatDamageDealt, unblockedAttacker, ct);
        }

        // Priority round after combat damage triggers
        if (_state.StackCount > 0)
            await RunPriorityAsync(ct);

        // Process deaths (state-based actions)
        var deadFromAttacker = ProcessCombatDeaths(attacker);
        var deadFromDefender = ProcessCombatDeaths(defender);

        // Fire Dies triggers for each creature that died
        foreach (var deadCard in deadFromAttacker.Concat(deadFromDefender))
        {
            await QueueBoardTriggersOnStackAsync(GameEvent.Dies, deadCard, ct);
        }

        // Priority round after dies triggers
        if (_state.StackCount > 0)
            await RunPriorityAsync(ct);

        // Recalculate effects and check if any player lost due to combat damage
        await OnBoardChangedAsync(ct);

        // End Combat
        _state.CombatStep = CombatStep.EndCombat;
        _state.Log("End of combat.");

        _state.CombatStep = CombatStep.None;
        _state.Combat = null;
    }

    /// <summary>
    /// Resolves combat damage. Returns the list of unblocked attacker cards that dealt damage to the player.
    /// </summary>
    private List<GameCard> ResolveCombatDamage(Player attacker, Player defender)
    {
        var unblockedAttackers = new List<GameCard>();

        foreach (var attackerId in _state.Combat!.Attackers)
        {
            var attackerCard = attacker.Battlefield.Cards.FirstOrDefault(c => c.Id == attackerId);
            if (attackerCard == null) continue;

            if (!_state.Combat.IsBlocked(attackerId))
            {
                // Unblocked: deal damage to target (planeswalker or player)
                var damage = attackerCard.Power ?? 0;
                if (damage > 0)
                {
                    var pwTargetId = _state.Combat.GetAttackerTarget(attackerId);
                    GameCard? targetPw = null;
                    if (pwTargetId.HasValue)
                        targetPw = defender.Battlefield.Cards.FirstOrDefault(c => c.Id == pwTargetId.Value && c.IsPlaneswalker);

                    if (targetPw != null)
                    {
                        // Deal damage to planeswalker by removing loyalty counters
                        var loyaltyToRemove = Math.Min(damage, targetPw.Loyalty);
                        for (int i = 0; i < loyaltyToRemove; i++)
                            targetPw.RemoveCounter(CounterType.Loyalty);

                        // If damage exceeds current loyalty, mark additional as removed
                        // (SBA will handle moving the PW to graveyard)
                        if (damage > loyaltyToRemove)
                        {
                            // Counters can't go below 0, but the PW is dead either way
                        }

                        _state.Log($"{attackerCard.Name} deals {damage} damage to {targetPw.Name}. ({targetPw.Loyalty} loyalty)");
                        unblockedAttackers.Add(attackerCard);

                        // Lifelink: controller gains life equal to damage dealt
                        if (attackerCard.ActiveKeywords.Contains(Keyword.Lifelink))
                        {
                            attacker.AdjustLife(damage);
                            _state.Log($"{attackerCard.Name} has lifelink — {attacker.Name} gains {damage} life. ({attacker.Life} life)");
                        }
                    }
                    else if (HasPlayerDamageProtection(defender.Id))
                    {
                        _state.Log($"{attackerCard.Name}'s {damage} damage to {defender.Name} is prevented (protection).");
                    }
                    else
                    {
                        defender.AdjustLife(-damage);
                        _state.Log($"{attackerCard.Name} deals {damage} damage to {defender.Name}. ({defender.Life} life)");
                        unblockedAttackers.Add(attackerCard);

                        // Lifelink: controller gains life equal to damage dealt
                        if (attackerCard.ActiveKeywords.Contains(Keyword.Lifelink))
                        {
                            attacker.AdjustLife(damage);
                            _state.Log($"{attackerCard.Name} has lifelink — {attacker.Name} gains {damage} life. ({attacker.Life} life)");
                        }
                    }
                }
            }
            else
            {
                // Blocked: deal damage to blockers in order, receive damage from all blockers
                var blockerOrder = _state.Combat.GetBlockerOrder(attackerId);
                var remainingDamage = attackerCard.Power ?? 0;

                foreach (var blockerId in blockerOrder)
                {
                    var blockerCard = defender.Battlefield.Cards.FirstOrDefault(c => c.Id == blockerId);
                    if (blockerCard == null || remainingDamage <= 0) continue;

                    // Assign lethal damage to this blocker, then move on
                    var lethal = (blockerCard.Toughness ?? 0) - blockerCard.DamageMarked;
                    var assigned = Math.Min(remainingDamage, Math.Max(lethal, 0));
                    if (assigned == 0 && remainingDamage > 0)
                        assigned = Math.Min(remainingDamage, 1); // Assign at least 1 if we have remaining damage
                    blockerCard.DamageMarked += assigned;
                    remainingDamage -= assigned;
                    _state.Log($"{attackerCard.Name} deals {assigned} damage to {blockerCard.Name}.");

                    // Lifelink on attacker dealing damage to blockers
                    if (assigned > 0 && attackerCard.ActiveKeywords.Contains(Keyword.Lifelink))
                    {
                        attacker.AdjustLife(assigned);
                        _state.Log($"{attackerCard.Name} has lifelink — {attacker.Name} gains {assigned} life. ({attacker.Life} life)");
                    }
                }

                // All blockers deal damage to attacker simultaneously
                foreach (var blockerId in blockerOrder)
                {
                    var blockerCard = defender.Battlefield.Cards.FirstOrDefault(c => c.Id == blockerId);
                    if (blockerCard == null) continue;

                    var blockerDamage = blockerCard.Power ?? 0;
                    if (blockerDamage > 0)
                    {
                        attackerCard.DamageMarked += blockerDamage;
                        _state.Log($"{blockerCard.Name} deals {blockerDamage} damage to {attackerCard.Name}.");

                        // Lifelink on blocker dealing damage to attacker
                        if (blockerCard.ActiveKeywords.Contains(Keyword.Lifelink))
                        {
                            defender.AdjustLife(blockerDamage);
                            _state.Log($"{blockerCard.Name} has lifelink — {defender.Name} gains {blockerDamage} life. ({defender.Life} life)");
                        }
                    }
                }
            }
        }

        return unblockedAttackers;
    }

    /// <summary>
    /// Processes combat deaths for a player. Returns the list of cards that died.
    /// </summary>
    private List<GameCard> ProcessCombatDeaths(Player player)
    {
        var dead = player.Battlefield.Cards
            .Where(c => c.IsCreature && c.Toughness.HasValue && c.DamageMarked >= c.Toughness.Value)
            .ToList();

        foreach (var card in dead)
        {
            TrackCreatureDeath(card, player);
            player.Battlefield.RemoveById(card.Id);
            // MTG rules: tokens go to graveyard then cease to exist (SBA 704.5d)
            player.Graveyard.Add(card);
            if (card.IsToken)
                player.Graveyard.RemoveById(card.Id);
            card.DamageMarked = 0;
            _state.Log($"{card.Name} dies.");
        }

        return dead;
    }

    private void TrackCreatureDeath(GameCard card, Player owner)
    {
        if (card.IsCreature && !card.IsToken)
            owner.CreaturesDiedThisTurn++;
    }

    /// <summary>
    /// Moves a card to graveyard, checking for replacement effects (e.g., Emrakul shuffle).
    /// </summary>
    public void MoveToGraveyardWithReplacement(GameCard card, Player owner)
    {
        if (CardDefinitions.TryGet(card.Name, out var def) && def.ShuffleGraveyardOnDeath)
        {
            // Shuffle this card + entire graveyard into library
            owner.Library.AddToTop(card);
            foreach (var graveyardCard in owner.Graveyard.Cards.ToList())
            {
                owner.Graveyard.Remove(graveyardCard);
                owner.Library.AddToTop(graveyardCard);
            }
            owner.Library.Shuffle();
            _state.Log($"{card.Name}'s graveyard replacement — {owner.Name} shuffles their graveyard into their library.");
        }
        else
        {
            owner.Graveyard.Add(card);
        }
    }

    /// <summary>
    /// Checks if a card can be targeted by a spell, considering protection abilities.
    /// </summary>
    public bool CanTargetWithSpell(GameCard target, GameCard spell)
    {
        if (target.ActiveKeywords.Contains(Keyword.ProtectionFromColoredSpells))
        {
            if (spell.ManaCost != null && spell.ManaCost.IsColored)
                return false;
        }
        return true;
    }

    public void ClearDamage()
    {
        foreach (var card in _state.Player1.Battlefield.Cards)
            card.DamageMarked = 0;
        foreach (var card in _state.Player2.Battlefield.Cards)
            card.DamageMarked = 0;
    }

    public void StripEndOfTurnEffects()
    {
        _state.ActiveEffects.RemoveAll(e => e.UntilEndOfTurn);
    }

    public void RemoveExpiredEffects()
    {
        _state.ActiveEffects.RemoveAll(e =>
            e.ExpiresOnTurnNumber != null && e.ExpiresOnTurnNumber <= _state.TurnNumber);
    }

    public void RecalculateState()
    {
        // Preserve temporary (UntilEndOfTurn) and expiring effects before rebuild
        var tempEffects = _state.ActiveEffects
            .Where(e => e.UntilEndOfTurn || e.ExpiresOnTurnNumber != null)
            .ToList();

        // Reset timestamp counter
        _state.NextEffectTimestamp = 1;

        // Rebuild ActiveEffects from CardDefinitions on the battlefield
        _state.ActiveEffects.Clear();
        RebuildActiveEffects(_state.Player1);
        RebuildActiveEffects(_state.Player2);

        // Graveyard-based abilities (e.g., Anger grants haste while in graveyard)
        RebuildGraveyardAbilities(_state.Player1);
        RebuildGraveyardAbilities(_state.Player2);

        // Emblem effects (command zone — permanent, cannot be removed)
        RebuildEmblemEffects(_state.Player1);
        RebuildEmblemEffects(_state.Player2);

        // Re-add temporary effects with fresh timestamps
        foreach (var temp in tempEffects)
        {
            _state.ActiveEffects.Add(temp with { Timestamp = _state.NextEffectTimestamp++ });
        }

        // Reset all effective values for both players
        foreach (var player in new[] { _state.Player1, _state.Player2 })
        {
            foreach (var card in player.Battlefield.Cards)
            {
                card.EffectivePower = null;
                card.EffectiveToughness = null;
                card.EffectiveCardTypes = null;
                card.ActiveKeywords.Clear();
                card.AbilitiesRemoved = false;

                // Restore ManaAbility from BaseManaAbility (continuous effects may override)
                if (card.BaseManaAbility != null)
                    card.ManaAbility = card.BaseManaAbility;
            }
            player.MaxLandDrops = 1;
        }

        // Build suppression tracking set
        var abilitiesRemovedFrom = new HashSet<Guid>();

        // === LAYER 4: Type-changing effects (BecomeCreature, OverrideLandType) ===
        foreach (var effect in _state.ActiveEffects
            .Where(e => e.Layer == EffectLayer.Layer4_TypeChanging)
            .OrderBy(e => e.Timestamp))
        {
            if (effect.StateCondition != null && !effect.StateCondition(_state))
                continue;

            if (effect.Type == ContinuousEffectType.OverrideLandType)
            {
                ApplyOverrideLandTypeEffect(effect, _state.Player1);
                ApplyOverrideLandTypeEffect(effect, _state.Player2);
            }
            else
            {
                ApplyBecomeCreatureEffect(effect, _state.Player1);
                ApplyBecomeCreatureEffect(effect, _state.Player2);
            }
        }

        // === LAYER 6: Ability add/remove ===
        // First: process RemoveAbilities effects
        foreach (var effect in _state.ActiveEffects
            .Where(e => e.Type == ContinuousEffectType.RemoveAbilities
                     && e.Layer == EffectLayer.Layer6_AbilityAddRemove)
            .OrderBy(e => e.Timestamp))
        {
            if (effect.StateCondition != null && !effect.StateCondition(_state))
                continue;

            foreach (var player in new[] { _state.Player1, _state.Player2 })
            {
                foreach (var card in player.Battlefield.Cards)
                {
                    if (!effect.Applies(card, player)) continue;
                    card.AbilitiesRemoved = true;
                    abilitiesRemovedFrom.Add(card.Id);
                }
            }
        }

        // Then: process GrantKeyword effects (skip if source creature lost abilities)
        foreach (var effect in _state.ActiveEffects
            .Where(e => e.Type == ContinuousEffectType.GrantKeyword
                     && (e.Layer == EffectLayer.Layer6_AbilityAddRemove || e.Layer == null))
            .OrderBy(e => e.Timestamp))
        {
            if (effect.StateCondition != null && !effect.StateCondition(_state))
                continue;

            // Skip if the SOURCE of this effect is a creature that lost its abilities
            if (abilitiesRemovedFrom.Contains(effect.SourceId))
                continue;

            ApplyKeywordEffect(effect, _state.Player1);
            ApplyKeywordEffect(effect, _state.Player2);
        }

        // === LAYER 7a: CDA (characteristic-defining abilities) ===
        foreach (var player in new[] { _state.Player1, _state.Player2 })
        {
            foreach (var card in player.Battlefield.Cards)
            {
                // Skip CDA if the creature lost abilities
                if (abilitiesRemovedFrom.Contains(card.Id)) continue;

                if (CardDefinitions.TryGet(card.Name, out var def))
                {
                    if (def.DynamicBasePower != null)
                        card.BasePower = def.DynamicBasePower(_state);
                    if (def.DynamicBaseToughness != null)
                        card.BaseToughness = def.DynamicBaseToughness(_state);
                }
            }
        }

        // === LAYER 7b: Set base P/T ===
        foreach (var effect in _state.ActiveEffects
            .Where(e => e.Type == ContinuousEffectType.SetBasePowerToughness
                     && e.Layer == EffectLayer.Layer7b_SetPT)
            .OrderBy(e => e.Timestamp))
        {
            if (effect.StateCondition != null && !effect.StateCondition(_state))
                continue;

            foreach (var player in new[] { _state.Player1, _state.Player2 })
            {
                foreach (var card in player.Battlefield.Cards)
                {
                    if (!effect.Applies(card, player)) continue;
                    if (effect.SetPower.HasValue)
                        card.EffectivePower = effect.SetPower.Value;
                    if (effect.SetToughness.HasValue)
                        card.EffectiveToughness = effect.SetToughness.Value;
                }
            }
        }

        // === LAYER 7c: Modify P/T (additive) ===
        foreach (var effect in _state.ActiveEffects
            .Where(e => e.Type == ContinuousEffectType.ModifyPowerToughness
                     && (e.Layer == EffectLayer.Layer7c_ModifyPT || e.Layer == null))
            .OrderBy(e => e.Timestamp))
        {
            if (effect.StateCondition != null && !effect.StateCondition(_state))
                continue;

            // Skip if the SOURCE of this effect is a creature that lost its abilities
            if (abilitiesRemovedFrom.Contains(effect.SourceId))
                continue;

            ApplyPowerToughnessEffect(effect, _state.Player1);
            ApplyPowerToughnessEffect(effect, _state.Player2);
        }

        // === LAYER 7d: +1/+1 counter adjustments (MTG Layer 7d) ===
        foreach (var player in new[] { _state.Player1, _state.Player2 })
        {
            foreach (var card in player.Battlefield.Cards)
            {
                var plusCounters = card.GetCounters(CounterType.PlusOnePlusOne);
                if (plusCounters > 0 && card.IsCreature)
                {
                    card.EffectivePower = (card.EffectivePower ?? card.BasePower ?? 0) + plusCounters;
                    card.EffectiveToughness = (card.EffectiveToughness ?? card.BaseToughness ?? 0) + plusCounters;
                }
            }
        }

        // === Non-layered effects ===
        foreach (var effect in _state.ActiveEffects.Where(e => e.Type == ContinuousEffectType.ExtraLandDrop))
        {
            var sourceOwner = _state.Player1.Battlefield.Cards.Any(c => c.Id == effect.SourceId)
                ? _state.Player1 : _state.Player2;
            sourceOwner.MaxLandDrops += effect.ExtraLandDrops;
        }
    }

    private void ApplyBecomeCreatureEffect(ContinuousEffect effect, Player player)
    {
        foreach (var card in player.Battlefield.Cards)
        {
            // "each other" exclusion (e.g. Opalescence) — skip unless ApplyToSelf is set
            if (!effect.ApplyToSelf && card.Id == effect.SourceId) continue;
            if (!effect.Applies(card, player)) continue;

            // Add Creature type
            card.EffectiveCardTypes = (card.EffectiveCardTypes ?? card.CardTypes) | CardType.Creature;

            // Set P/T to CMC (e.g. Opalescence)
            if (effect.SetPowerToughnessToCMC && card.ManaCost != null)
            {
                var cmc = card.ManaCost.ConvertedManaCost;
                card.EffectivePower = cmc;
                card.EffectiveToughness = cmc;
            }

            // Set P/T to explicit values (e.g. Kaito creature mode)
            if (effect.SetPower.HasValue)
                card.EffectivePower = effect.SetPower.Value;
            if (effect.SetToughness.HasValue)
                card.EffectiveToughness = effect.SetToughness.Value;
        }
    }

    private void ApplyOverrideLandTypeEffect(ContinuousEffect effect, Player player)
    {
        foreach (var card in player.Battlefield.Cards)
        {
            if (!effect.Applies(card, player)) continue;

            // Blood Moon: nonbasic lands become Mountains (produce Red mana only)
            card.ManaAbility = ManaAbility.Fixed(ManaColor.Red);
        }
    }

    private void ApplyPowerToughnessEffect(ContinuousEffect effect, Player player)
    {
        foreach (var card in player.Battlefield.Cards)
        {
            if (card.Id == effect.SourceId) continue; // lords don't buff themselves
            if (!effect.Applies(card, player)) continue;

            card.EffectivePower = (card.EffectivePower ?? card.BasePower ?? 0) + effect.PowerMod;
            card.EffectiveToughness = (card.EffectiveToughness ?? card.BaseToughness ?? 0) + effect.ToughnessMod;
        }
    }

    private void ApplyKeywordEffect(ContinuousEffect effect, Player player)
    {
        // ControllerOnly: skip if the source is not on this player's battlefield
        if (effect.ControllerOnly && !player.Battlefield.Contains(effect.SourceId))
            return;

        foreach (var card in player.Battlefield.Cards)
        {
            if (effect.ExcludeSelf && card.Id == effect.SourceId) continue;
            if (!effect.Applies(card, player)) continue;
            if (effect.GrantedKeyword.HasValue)
                card.ActiveKeywords.Add(effect.GrantedKeyword.Value);
        }
    }

    private void RebuildActiveEffects(Player player)
    {
        foreach (var card in player.Battlefield.Cards)
        {
            if (!CardDefinitions.TryGet(card.Name, out var def)) continue;
            foreach (var templateEffect in def.ContinuousEffects)
            {
                var effect = templateEffect with
                {
                    SourceId = card.Id,
                    Timestamp = _state.NextEffectTimestamp++
                };
                _state.ActiveEffects.Add(effect);
            }
        }
    }

    private void RebuildGraveyardAbilities(Player player)
    {
        foreach (var card in player.Graveyard.Cards)
        {
            if (!CardDefinitions.TryGet(card.Name, out var def)) continue;
            if (def.GraveyardAbilities.Count == 0) continue;

            var ownerId = player.Id;
            foreach (var ability in def.GraveyardAbilities)
            {
                var originalApplies = ability.Applies;
                _state.ActiveEffects.Add(ability with
                {
                    SourceId = card.Id,
                    Timestamp = _state.NextEffectTimestamp++,
                    Applies = (c, p) => p.Id == ownerId && originalApplies(c, p)
                });
            }
        }
    }

    private void RebuildEmblemEffects(Player player)
    {
        var ownerId = player.Id;
        foreach (var emblem in player.Emblems)
        {
            var originalApplies = emblem.Effect.Applies;
            var effect = emblem.Effect with
            {
                Timestamp = _state.NextEffectTimestamp++,
                // Scope ControllerOnly emblems to the owning player (same pattern as graveyard abilities)
                Applies = emblem.Effect.ControllerOnly
                    ? (c, p) => p.Id == ownerId && originalApplies(c, p)
                    : originalApplies
            };
            _state.ActiveEffects.Add(effect);
        }
    }

    /// <summary>Test accessor for CollectBoardTriggers.</summary>
    internal List<TriggeredAbilityStackObject> CollectBoardTriggersForTest(GameEvent evt, GameCard? relevantCard, Player player)
        => CollectBoardTriggers(evt, relevantCard, player);

    internal int ComputeCostModification(GameCard card, Player caster)
    {
        return _state.ActiveEffects
            .Where(e => e.Type == ContinuousEffectType.ModifyCost
                   && e.CostApplies != null
                   && e.CostApplies(card)
                   && IsCostEffectApplicable(e, caster))
            .Sum(e => e.CostMod);
    }

    private bool IsCostEffectApplicable(ContinuousEffect effect, Player caster)
    {
        if (!effect.CostAppliesToOpponent) return true;

        // For opponent-only effects, find who controls the source
        var effectController = _state.Player1.Battlefield.Contains(effect.SourceId) ? _state.Player1
            : _state.Player2.Battlefield.Contains(effect.SourceId) ? _state.Player2 : null;

        // Only apply if the caster is the opponent (not the controller)
        return effectController != null && effectController.Id != caster.Id;
    }

    internal async Task OnBoardChangedAsync(CancellationToken ct = default)
    {
        RecalculateState();
        await CheckStateBasedActionsAsync(ct);
    }

    internal async Task CheckStateBasedActionsAsync(CancellationToken ct = default)
    {
        // SBAs loop until no more actions are taken (MTG 704.3)
        bool anyActionTaken;
        do
        {
            if (_state.IsGameOver) return;
            anyActionTaken = false;

            // Life check (MTG 704.5a)
            bool p1Dead = _state.Player1.Life <= 0;
            bool p2Dead = _state.Player2.Life <= 0;

            if (p1Dead && p2Dead)
            {
                _state.IsGameOver = true;
                _state.Winner = null; // draw
                _state.Log($"Both players lose — {_state.Player1.Name} ({_state.Player1.Life} life) and {_state.Player2.Name} ({_state.Player2.Life} life).");
                return;
            }
            else if (p1Dead)
            {
                _state.IsGameOver = true;
                _state.Winner = _state.Player2.Name;
                _state.Log($"{_state.Player1.Name} loses — life reached {_state.Player1.Life}.");
                return;
            }
            else if (p2Dead)
            {
                _state.IsGameOver = true;
                _state.Winner = _state.Player1.Name;
                _state.Log($"{_state.Player2.Name} loses — life reached {_state.Player2.Life}.");
                return;
            }

            // Legendary rule (MTG 704.5j)
            bool legendaryP1 = await CheckLegendaryRuleAsync(_state.Player1, ct);
            bool legendaryP2 = await CheckLegendaryRuleAsync(_state.Player2, ct);
            if (legendaryP1 || legendaryP2)
                anyActionTaken = true;

            // Zero-or-less toughness (MTG 704.5f)
            bool lethalP1 = await CheckLethalToughness(_state.Player1, ct);
            bool lethalP2 = await CheckLethalToughness(_state.Player2, ct);
            if (lethalP1 || lethalP2)
                anyActionTaken = true;

            // Lethal damage (MTG 704.5g) — creatures with damage >= toughness die
            var lethalDamageDeaths = new List<GameCard>();
            await CheckLethalDamage(_state.Player1, lethalDamageDeaths, ct);
            await CheckLethalDamage(_state.Player2, lethalDamageDeaths, ct);
            if (lethalDamageDeaths.Count > 0)
            {
                anyActionTaken = true;
                // Fire Dies triggers for each creature that died from lethal damage
                foreach (var deadCard in lethalDamageDeaths)
                    await QueueBoardTriggersOnStackAsync(GameEvent.Dies, deadCard, ct);
            }

            // Aura detachment (MTG 704.5m) — aura goes to graveyard if enchanted permanent is gone
            foreach (var p in new[] { _state.Player1, _state.Player2 })
            {
                var auras = p.Battlefield.Cards.Where(c => c.AttachedTo.HasValue).ToList();
                foreach (var aura in auras)
                {
                    var targetExists = _state.Player1.Battlefield.Contains(aura.AttachedTo!.Value)
                        || _state.Player2.Battlefield.Contains(aura.AttachedTo!.Value);
                    if (!targetExists)
                    {
                        await FireLeaveBattlefieldTriggersAsync(aura, p, ct);
                        p.Battlefield.RemoveById(aura.Id);
                        p.Graveyard.Add(aura);
                        _state.Log($"{aura.Name} falls off (enchanted permanent left battlefield).");
                        anyActionTaken = true;
                    }
                }
            }

            // SBA: Planeswalker with 0 or less loyalty → graveyard (MTG 704.5i)
            foreach (var player in new[] { _state.Player1, _state.Player2 })
            {
                var dyingPws = player.Battlefield.Cards
                    .Where(c => c.IsPlaneswalker && c.Loyalty <= 0)
                    .ToList();

                foreach (var pw in dyingPws)
                {
                    await FireLeaveBattlefieldTriggersAsync(pw, player, ct);
                    player.Battlefield.Remove(pw);
                    player.Graveyard.Add(pw);
                    _state.Log($"{pw.Name} is put into {player.Name}'s graveyard (0 loyalty).");
                    anyActionTaken = true;
                }
            }

            // If anything changed, recalculate effects before looping
            if (anyActionTaken)
                RecalculateState();

        } while (anyActionTaken);
    }

    private async Task<bool> CheckLegendaryRuleAsync(Player player, CancellationToken ct)
    {
        var legendaries = player.Battlefield.Cards
            .Where(c => c.IsLegendary)
            .GroupBy(c => c.Name)
            .Where(g => g.Count() > 1)
            .ToList();

        if (legendaries.Count == 0) return false;

        foreach (var group in legendaries)
        {
            var duplicates = group.ToList();
            var chosen = await player.DecisionHandler.ChooseTarget(
                $"Legendary rule: choose which {duplicates[0].Name} to keep",
                duplicates, player.Id, ct);
            var chosenId = chosen?.CardId ?? duplicates[0].Id;

            foreach (var card in duplicates.Where(c => c.Id != chosenId))
            {
                await FireLeaveBattlefieldTriggersAsync(card, player, ct);
                player.Battlefield.RemoveById(card.Id);
                player.Graveyard.Add(card);
                _state.Log($"{card.Name} is put into graveyard (legendary rule).");
            }
        }

        return true;
    }

    private async Task<bool> CheckLethalToughness(Player player, CancellationToken ct)
    {
        var dead = player.Battlefield.Cards
            .Where(c => c.IsCreature && (c.Toughness ?? 1) <= 0)
            .ToList();

        foreach (var card in dead)
        {
            TrackCreatureDeath(card, player);
            await FireLeaveBattlefieldTriggersAsync(card, player, ct);
            player.Battlefield.RemoveById(card.Id);
            player.Graveyard.Add(card);
            _state.Log($"{card.Name} dies (0 toughness).");
        }

        return dead.Count > 0;
    }

    private async Task CheckLethalDamage(Player player, List<GameCard> deaths, CancellationToken ct)
    {
        var dead = player.Battlefield.Cards
            .Where(c => c.IsCreature && c.Toughness.HasValue && c.DamageMarked >= c.Toughness.Value && c.Toughness.Value > 0)
            .ToList();

        foreach (var card in dead)
        {
            TrackCreatureDeath(card, player);
            await FireLeaveBattlefieldTriggersAsync(card, player, ct);
            player.Battlefield.RemoveById(card.Id);
            // MTG rules: tokens go to graveyard then cease to exist (SBA 704.5d)
            player.Graveyard.Add(card);
            if (card.IsToken)
                player.Graveyard.RemoveById(card.Id);
            card.DamageMarked = 0;
            _state.Log($"{card.Name} dies (lethal damage).");
            deaths.Add(card);
        }
    }

    /// <summary>Applies EntersWithCounters from CardDefinitions immediately when a permanent enters the battlefield.</summary>
    internal void ApplyEntersWithCounters(GameCard card)
    {
        if (CardDefinitions.TryGet(card.Name, out var def))
        {
            if (def.EntersWithCounters != null)
            {
                foreach (var (type, count) in def.EntersWithCounters)
                {
                    card.AddCounters(type, count);
                    _state.Log($"{card.Name} enters with {count} {type} counter(s).");
                }
            }

            // Planeswalker loyalty setup
            if (def.StartingLoyalty.HasValue && card.IsPlaneswalker)
            {
                card.AddCounters(CounterType.Loyalty, def.StartingLoyalty.Value);
                _state.Log($"{card.Name} enters with {def.StartingLoyalty.Value} loyalty.");
            }
        }
    }

    /// <summary>Queues Self triggers for a specific card onto the stack.</summary>
    internal Task QueueSelfTriggersOnStackAsync(GameEvent evt, GameCard source, Player controller, CancellationToken ct = default)
    {
        if (source.Triggers.Count == 0) return Task.CompletedTask;
        // Suppression: if source creature lost abilities, skip self triggers
        if (source.IsCreature && source.AbilitiesRemoved) return Task.CompletedTask;

        foreach (var trigger in source.Triggers)
        {
            if (trigger.Event != evt) continue;
            if (trigger.Condition != TriggerCondition.Self) continue;

            _state.Log($"{source.Name} triggers: {trigger.Effect.GetType().Name.Replace("Effect", "")}");
            _state.StackPush(new TriggeredAbilityStackObject(source, controller.Id, trigger.Effect));
        }

        return Task.CompletedTask;
    }

    /// <summary>Queues board-wide triggers onto the stack with APNAP ordering.</summary>
    internal Task QueueBoardTriggersOnStackAsync(GameEvent evt, GameCard? relevantCard, CancellationToken ct = default)
    {
        var activePlayer = _state.ActivePlayer;
        var nonActivePlayer = _state.GetOpponent(activePlayer);

        var activeTriggers = CollectBoardTriggers(evt, relevantCard, activePlayer);
        var nonActiveTriggers = CollectBoardTriggers(evt, relevantCard, nonActivePlayer);

        // Active player's triggers go on stack first (resolve last — correct per APNAP)
        foreach (var t in activeTriggers)
            _state.StackPush(t);
        // Non-active player's triggers on top (resolve first via LIFO)
        foreach (var t in nonActiveTriggers)
            _state.StackPush(t);

        return Task.CompletedTask;
    }

    private List<TriggeredAbilityStackObject> CollectBoardTriggers(GameEvent evt, GameCard? relevantCard, Player player)
    {
        var result = new List<TriggeredAbilityStackObject>();
        var permanents = player.Battlefield.Cards.ToList();

        foreach (var permanent in permanents)
        {
            var triggers = permanent.Triggers.Count > 0
                ? permanent.Triggers
                : (CardDefinitions.TryGet(permanent.Name, out var def) ? def.Triggers : []);
            if (triggers.Count == 0) continue;
            // Suppression: if this permanent is a creature that lost abilities, skip its triggers
            if (permanent.IsCreature && permanent.AbilitiesRemoved) continue;

            foreach (var trigger in triggers)
            {
                if (trigger.Event != evt) continue;
                if (trigger.Condition == TriggerCondition.Self) continue;

                bool matches = trigger.Condition switch
                {
                    TriggerCondition.AnyCreatureDies =>
                        evt == GameEvent.Dies && relevantCard != null && relevantCard.IsCreature,
                    TriggerCondition.ControllerCastsEnchantment =>
                        evt == GameEvent.SpellCast
                        && relevantCard != null
                        && relevantCard.CardTypes.HasFlag(CardType.Enchantment)
                        && _state.ActivePlayer == player,
                    TriggerCondition.SelfDealsCombatDamage =>
                        evt == GameEvent.CombatDamageDealt
                        && relevantCard != null
                        && relevantCard.Id == permanent.Id,
                    TriggerCondition.Upkeep =>
                        evt == GameEvent.Upkeep
                        && _state.ActivePlayer == player,
                    TriggerCondition.ControllerCastsNoncreature =>
                        evt == GameEvent.SpellCast
                        && relevantCard != null
                        && !relevantCard.CardTypes.HasFlag(CardType.Creature)
                        && _state.ActivePlayer == player,
                    TriggerCondition.AnyPlayerCastsSpell =>
                        evt == GameEvent.SpellCast,
                    TriggerCondition.AnyUpkeep =>
                        evt == GameEvent.Upkeep,
                    TriggerCondition.AnySpellCastCmc3OrLess =>
                        evt == GameEvent.SpellCast
                        && relevantCard != null
                        && (relevantCard.ManaCost?.ConvertedManaCost ?? 0) <= 3,
                    TriggerCondition.ControllerPlaysAnotherLand =>
                        evt == GameEvent.LandPlayed
                        && relevantCard != null
                        && relevantCard.Id != permanent.Id
                        && _state.ActivePlayer == player,
                    TriggerCondition.SelfAttacks => false,
                    _ => false,
                };

                if (matches)
                {
                    _state.Log($"{permanent.Name} triggers: {trigger.Effect.GetType().Name.Replace("Effect", "")}");
                    var stackObj = new TriggeredAbilityStackObject(permanent, player.Id, trigger.Effect);

                    // For spell-cast triggers that target the caster
                    if (trigger.Condition == TriggerCondition.AnySpellCastCmc3OrLess)
                        stackObj = new TriggeredAbilityStackObject(permanent, player.Id, trigger.Effect)
                            { TargetPlayerId = _state.ActivePlayer.Id };

                    result.Add(stackObj);
                }
            }
        }

        return result;
    }

    /// <summary>Queues attack triggers onto the stack.</summary>
    internal Task QueueAttackTriggersOnStackAsync(GameCard attacker, CancellationToken ct = default)
    {
        // Suppression: if attacker lost abilities, skip attack triggers
        if (attacker.AbilitiesRemoved) return Task.CompletedTask;

        var player = _state.ActivePlayer;
        var triggers = attacker.Triggers.Count > 0
            ? attacker.Triggers
            : (CardDefinitions.TryGet(attacker.Name, out var def) ? def.Triggers : []);

        foreach (var trigger in triggers)
        {
            if (trigger.Condition != TriggerCondition.SelfAttacks) continue;

            _state.Log($"{attacker.Name} triggers: {trigger.Effect.GetType().Name.Replace("Effect", "")}");
            _state.StackPush(new TriggeredAbilityStackObject(attacker, player.Id, trigger.Effect));
        }

        return Task.CompletedTask;
    }

    /// <summary>Queues cast triggers from the spell itself (e.g., Emrakul extra turn on cast).</summary>
    internal Task QueueSelfCastTriggersAsync(GameCard card, Player controller, CancellationToken ct)
    {
        // Check triggers on the card instance first, then fall back to CardDefinitions
        var triggers = card.Triggers.Count > 0
            ? card.Triggers
            : (CardDefinitions.TryGet(card.Name, out var def) ? def.Triggers : []);

        foreach (var trigger in triggers)
        {
            if (trigger.Event == GameEvent.SpellCast && trigger.Condition == TriggerCondition.SelfIsCast)
            {
                _state.StackPush(new TriggeredAbilityStackObject(card, controller.Id, trigger.Effect));
                _state.Log($"{card.Name}'s cast trigger goes on the stack.");
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>Queues triggers from cards in active player's graveyard (e.g., Squee).</summary>
    internal Task QueueGraveyardTriggersOnStackAsync(GameEvent evt, CancellationToken ct = default)
    {
        var activePlayer = _state.ActivePlayer;

        foreach (var card in activePlayer.Graveyard.Cards)
        {
            var triggers = card.Triggers.Count > 0
                ? card.Triggers
                : (CardDefinitions.TryGet(card.Name, out var def) ? def.Triggers : []);

            foreach (var trigger in triggers)
            {
                if (trigger.Event != evt) continue;
                if (trigger.Condition != TriggerCondition.SelfInGraveyardDuringUpkeep) continue;

                _state.Log($"{card.Name} triggers from graveyard: {trigger.Effect.GetType().Name.Replace("Effect", "")}");
                _state.StackPush(new TriggeredAbilityStackObject(card, activePlayer.Id, trigger.Effect));
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>Queues echo triggers for permanents with unpaid echo cost.</summary>
    internal Task QueueEchoTriggersOnStackAsync(CancellationToken ct = default)
    {
        var activePlayer = _state.ActivePlayer;

        foreach (var card in activePlayer.Battlefield.Cards.ToList())
        {
            if (card.EchoPaid) continue;
            // Suppression: if creature lost abilities, skip echo
            if (card.AbilitiesRemoved) continue;
            if (!CardDefinitions.TryGet(card.Name, out var def) || def.EchoCost == null) continue;

            _state.Log($"{card.Name} echo trigger.");
            _state.StackPush(new TriggeredAbilityStackObject(card, activePlayer.Id, new Triggers.Effects.EchoEffect(def.EchoCost)));
        }

        return Task.CompletedTask;
    }

    /// <summary>Queues delayed triggers onto the stack and removes them.</summary>
    internal Task QueueDelayedTriggersOnStackAsync(GameEvent evt, CancellationToken ct = default)
    {
        var toFire = _state.DelayedTriggers.Where(d => d.FireOn == evt).ToList();
        foreach (var delayed in toFire)
        {
            var controller = delayed.ControllerId == _state.Player1.Id ? _state.Player1 : _state.Player2;
            var source = new GameCard { Name = "Delayed Trigger" };
            _state.StackPush(new TriggeredAbilityStackObject(source, controller.Id, delayed.Effect));
            _state.DelayedTriggers.Remove(delayed);
        }

        return Task.CompletedTask;
    }

    internal Task FireLeaveBattlefieldTriggersAsync(GameCard card, Player controller, CancellationToken ct)
    {
        // Track revolt — a permanent controlled by this player left the battlefield
        controller.PermanentLeftBattlefieldThisTurn = true;

        if (!CardDefinitions.TryGet(card.Name, out var def)) return Task.CompletedTask;

        foreach (var trigger in def.Triggers)
        {
            if (trigger.Condition == TriggerCondition.SelfLeavesBattlefield)
            {
                _state.Log($"{card.Name} triggers: {trigger.Effect.GetType().Name.Replace("Effect", "")}");
                _state.StackPush(new TriggeredAbilityStackObject(card, controller.Id, trigger.Effect));
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>Snapshots both players' battlefield card IDs for ETB diffing.</summary>
    private (HashSet<Guid> p1, HashSet<Guid> p2) SnapshotBattlefields() =>
        (new HashSet<Guid>(_state.Player1.Battlefield.Cards.Select(c => c.Id)),
         new HashSet<Guid>(_state.Player2.Battlefield.Cards.Select(c => c.Id)));

    /// <summary>Fires ETB triggers for any permanents that appeared on either battlefield since the snapshot.</summary>
    private async Task FireEtbForNewPermanentsAsync(HashSet<Guid> p1Before, HashSet<Guid> p2Before, CancellationToken ct)
    {
        foreach (var card in _state.Player1.Battlefield.Cards.Where(c => !p1Before.Contains(c.Id)))
        {
            ApplyEntersWithCounters(card);
            await QueueSelfTriggersOnStackAsync(GameEvent.EnterBattlefield, card, _state.Player1, ct);
        }
        foreach (var card in _state.Player2.Battlefield.Cards.Where(c => !p2Before.Contains(c.Id)))
        {
            ApplyEntersWithCounters(card);
            await QueueSelfTriggersOnStackAsync(GameEvent.EnterBattlefield, card, _state.Player2, ct);
        }
    }

    /// <summary>Resolves all items on the stack (for testing).</summary>
    internal async Task ResolveAllTriggersAsync(CancellationToken ct = default)
    {
        while (_state.StackCount > 0)
            await ResolveTopOfStackAsync(ct);
    }

    internal async Task RunPriorityAsync(CancellationToken ct = default)
    {
        _state.PriorityPlayer = _state.ActivePlayer;
        bool activePlayerPassed = false;
        bool nonActivePlayerPassed = false;
        int consecutiveRejections = 0;

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var action = await _state.PriorityPlayer.DecisionHandler
                .GetAction(_state, _state.PriorityPlayer.Id, ct);

            if (action.Type == ActionType.PassPriority)
            {
                if (_state.PriorityPlayer == _state.ActivePlayer)
                    activePlayerPassed = true;
                else
                    nonActivePlayerPassed = true;

                if (activePlayerPassed && nonActivePlayerPassed)
                {
                    if (_state.StackCount > 0)
                    {
                        await ResolveTopOfStackAsync(ct);
                        _state.PriorityPlayer = _state.ActivePlayer;
                        activePlayerPassed = false;
                        nonActivePlayerPassed = false;
                        continue;
                    }
                    return;
                }

                _state.PriorityPlayer = _state.GetOpponent(_state.PriorityPlayer);
            }
            else
            {
                var player = action.PlayerId == _state.Player1.Id ? _state.Player1 : _state.Player2;
                var historyCount = player.ActionHistory.Count;
                await ExecuteAction(action, ct);

                // Only reset pass flags if the action actually did something
                // (ExecuteAction pushes to ActionHistory on success, returns early on failure)
                if (player.ActionHistory.Count > historyCount)
                {
                    // Mana abilities don't use the stack and don't pass priority (MTG rule 605).
                    // The player retains priority and can continue acting.
                    if (action.IsManaAbility)
                    {
                        consecutiveRejections = 0;
                        // Priority stays with the same player — no flag reset
                    }
                    else
                    {
                        activePlayerPassed = false;
                        nonActivePlayerPassed = false;
                        consecutiveRejections = 0;
                        _state.PriorityPlayer = _state.ActivePlayer;
                    }
                }
                else
                {
                    // Action was rejected — re-prompt same player so they can tap mana
                    // or choose a different action. After 3 consecutive rejections, auto-pass
                    // to prevent infinite loops with AI bots.
                    consecutiveRejections++;
                    if (consecutiveRejections >= 3)
                    {
                        consecutiveRejections = 0;
                        if (_state.PriorityPlayer == _state.ActivePlayer)
                            activePlayerPassed = true;
                        else
                            nonActivePlayerPassed = true;
                        _state.PriorityPlayer = _state.GetOpponent(_state.PriorityPlayer);
                    }
                }
            }
        }
    }

    private async Task ResolveTopOfStackAsync(CancellationToken ct = default)
    {
        if (_state.StackCount == 0) return;

        var top = _state.StackPopTop();
        if (top == null) return;
        var controller = _state.GetPlayer(top.ControllerId);

        if (top is TriggeredAbilityStackObject triggered)
        {
            _state.Log($"Resolving triggered ability: {triggered.Source.Name} — {triggered.Effect.GetType().Name.Replace("Effect", "")}");
            var context = new EffectContext(_state, controller, triggered.Source, controller.DecisionHandler)
            {
                Target = triggered.Target,
                TargetPlayerId = triggered.TargetPlayerId,
                FireLeaveBattlefieldTriggers = async card =>
                {
                    var ctrl = _state.Player1.Battlefield.Contains(card.Id) ? _state.Player1
                        : _state.Player2.Battlefield.Contains(card.Id) ? _state.Player2 : null;
                    if (ctrl != null) await FireLeaveBattlefieldTriggersAsync(card, ctrl, ct);
                },
            };
            var (p1Before, p2Before) = SnapshotBattlefields();
            await triggered.Effect.Execute(context, ct);
            await FireEtbForNewPermanentsAsync(p1Before, p2Before, ct);
            await OnBoardChangedAsync(ct);
            return;
        }

        if (top is ActivatedLoyaltyAbilityStackObject loyaltyAbility)
        {
            var loyaltyContext = new EffectContext(_state, controller, loyaltyAbility.Source, controller.DecisionHandler)
            {
                Target = loyaltyAbility.Target,
                TargetPlayerId = loyaltyAbility.TargetPlayerId,
                FireLeaveBattlefieldTriggers = async card =>
                {
                    var ctrl = _state.Player1.Battlefield.Contains(card.Id) ? _state.Player1
                        : _state.Player2.Battlefield.Contains(card.Id) ? _state.Player2 : null;
                    if (ctrl != null) await FireLeaveBattlefieldTriggersAsync(card, ctrl, ct);
                },
            };
            var (p1BeforeLoyalty, p2BeforeLoyalty) = SnapshotBattlefields();
            await loyaltyAbility.Effect.Execute(loyaltyContext, ct);
            await FireEtbForNewPermanentsAsync(p1BeforeLoyalty, p2BeforeLoyalty, ct);
            _state.Log($"Resolved {loyaltyAbility.Source.Name} loyalty ability: {loyaltyAbility.Description}");
            await OnBoardChangedAsync(ct);
            return;
        }

        if (top is StackObject spell)
        {
            // Adventure spell resolution: run adventure effect, then exile with IsOnAdventure
            if (spell.IsAdventure)
            {
                if (CardDefinitions.TryGet(spell.Card.Name, out var advDef) && advDef.Adventure?.Effect != null)
                {
                    _state.Log($"Resolving {advDef.Adventure.Name} (adventure of {spell.Card.Name}).");

                    // Target legality check
                    if (spell.Targets.Count > 0)
                    {
                        var allAdvTargetsLegal = true;
                        foreach (var target in spell.Targets)
                        {
                            if (target.Zone == ZoneType.None) continue;
                            var targetOwner = _state.GetPlayer(target.PlayerId);
                            var targetZone = targetOwner.GetZone(target.Zone);
                            if (!targetZone.Contains(target.CardId))
                            {
                                allAdvTargetsLegal = false;
                                break;
                            }
                        }

                        if (!allAdvTargetsLegal)
                        {
                            _state.Log($"{advDef.Adventure.Name} fizzles (illegal target).");
                            // Adventure fizzle: card goes to exile on adventure regardless
                            spell.Card.IsOnAdventure = true;
                            controller.Exile.Add(spell.Card);
                            _state.Log($"{spell.Card.Name} is exiled (adventure).");
                            return;
                        }
                    }

                    await advDef.Adventure.Effect.ResolveAsync(_state, spell, controller.DecisionHandler, ct);
                }
                else
                {
                    _state.Log($"Resolving adventure of {spell.Card.Name}.");
                }

                // Adventure resolution: card goes to exile with IsOnAdventure
                spell.Card.IsOnAdventure = true;
                controller.Exile.Add(spell.Card);
                _state.Log($"{spell.Card.Name} is exiled (adventure).");
                await OnBoardChangedAsync(ct);
                return;
            }

            _state.Log($"Resolving {spell.Card.Name}.");

            if (CardDefinitions.TryGet(spell.Card.Name, out var def) && def.Effect != null)
            {
                if (spell.Targets.Count > 0)
                {
                    var allTargetsLegal = true;
                    foreach (var target in spell.Targets)
                    {
                        // Player targets (zone == None) are always legal
                        if (target.Zone == ZoneType.None)
                            continue;

                        // Stack targets — check if the target spell is still on the stack
                        if (target.Zone == ZoneType.Stack)
                        {
                            if (!_state.Stack.Any(s => s is StackObject so && so.Card.Id == target.CardId))
                            {
                                allTargetsLegal = false;
                                break;
                            }
                            continue;
                        }

                        var targetOwner = _state.GetPlayer(target.PlayerId);
                        var targetZone = targetOwner.GetZone(target.Zone);
                        if (!targetZone.Contains(target.CardId))
                        {
                            allTargetsLegal = false;
                            break;
                        }
                    }

                    if (!allTargetsLegal)
                    {
                        _state.Log($"{spell.Card.Name} fizzles (illegal target).");
                        if (spell.IsFlashback)
                        {
                            controller.Exile.Add(spell.Card);
                            _state.Log($"{spell.Card.Name} is exiled (flashback).");
                        }
                        else
                        {
                            controller.Graveyard.Add(spell.Card);
                        }
                        return;
                    }
                }

                var (p1BeforeSpell, p2BeforeSpell) = SnapshotBattlefields();
                await def.Effect.ResolveAsync(_state, spell, controller.DecisionHandler, ct);
                await FireEtbForNewPermanentsAsync(p1BeforeSpell, p2BeforeSpell, ct);
                if (spell.IsFlashback)
                {
                    controller.Exile.Add(spell.Card);
                    _state.Log($"{spell.Card.Name} is exiled (flashback).");
                }
                else
                {
                    controller.Graveyard.Add(spell.Card);
                }
                await OnBoardChangedAsync(ct);
            }
            else
            {
                if (spell.Card.IsCreature || spell.Card.CardTypes.HasFlag(CardType.Enchantment)
                    || spell.Card.CardTypes.HasFlag(CardType.Artifact)
                    || spell.Card.IsPlaneswalker)
                {
                    spell.Card.TurnEnteredBattlefield = _state.TurnNumber;
                    if (spell.Card.EntersTapped) spell.Card.IsTapped = true;
                    controller.Battlefield.Add(spell.Card);

                    // Aura attachment on stack resolution
                    await TryAttachAuraAsync(spell.Card, controller, ct);

                    ApplyEntersWithCounters(spell.Card);
                    await QueueSelfTriggersOnStackAsync(GameEvent.EnterBattlefield, spell.Card, controller, ct);
                    await OnBoardChangedAsync(ct);
                }
                else
                {
                    if (spell.IsFlashback)
                    {
                        controller.Exile.Add(spell.Card);
                        _state.Log($"{spell.Card.Name} is exiled (flashback).");
                    }
                    else
                    {
                        controller.Graveyard.Add(spell.Card);
                    }
                }
            }
        }
    }

    internal async Task RunMulliganAsync(Player player, CancellationToken ct = default)
    {
        int mulliganCount = 0;

        if (player.Hand.Count == 0)
            DrawCards(player, 7);

        const int maxMulligans = 7;

        while (mulliganCount < maxMulligans)
        {
            var decision = await player.DecisionHandler
                .GetMulliganDecision(player.Hand.Cards, mulliganCount, ct);

            if (decision == MulliganDecision.Keep)
            {
                if (mulliganCount > 0)
                {
                    var cardsToBottom = await player.DecisionHandler
                        .ChooseCardsToBottom(player.Hand.Cards, mulliganCount, ct);

                    foreach (var card in cardsToBottom)
                    {
                        player.Hand.RemoveById(card.Id);
                        player.Library.AddToBottom(card);
                    }
                }

                _state.Log($"{player.Name} keeps hand of {player.Hand.Count} cards (mulliganed {mulliganCount} times).");
                return;
            }

            mulliganCount++;
            ReturnHandToLibrary(player);
            player.Library.Shuffle();
            DrawCards(player, 7);
        }

        ReturnHandToLibrary(player);
        _state.Log($"{player.Name} mulliganed to 0 cards.");
    }

    internal void DrawCards(Player player, int count, bool isDrawStepDraw = false)
    {
        for (int i = 0; i < count; i++)
        {
            var card = player.Library.DrawFromTop();
            if (card != null)
            {
                player.Hand.Add(card);
                player.DrawsThisTurn++;

                if (isDrawStepDraw && !player.DrawStepDrawExempted)
                {
                    player.DrawStepDrawExempted = true;
                    // First draw of draw step is exempt from draw triggers
                }
                else
                {
                    // Fire draw triggers (e.g., Orcish Bowmasters)
                    QueueDrawTriggers(player);
                }
            }
            else
            {
                var winner = _state.GetOpponent(player);
                _state.IsGameOver = true;
                _state.Winner = winner.Name;
                _state.Log($"{player.Name} loses — cannot draw from an empty library.");
                return;
            }
        }
    }

    private void QueueDrawTriggers(Player drawingPlayer)
    {
        foreach (var player in new[] { _state.Player1, _state.Player2 })
        {
            foreach (var card in player.Battlefield.Cards)
            {
                if (card.AbilitiesRemoved) continue;
                foreach (var trigger in card.Triggers)
                {
                    if (trigger.Event != GameEvent.DrawCard) continue;

                    if (trigger.Condition == TriggerCondition.OpponentDrawsExceptFirst)
                    {
                        if (drawingPlayer.Id == player.Id) continue; // Only opponent draws
                        _state.Log($"{card.Name} triggers on {drawingPlayer.Name}'s draw.");
                        _state.StackPush(new TriggeredAbilityStackObject(card, player.Id, trigger.Effect));
                    }
                    else if (trigger.Condition == TriggerCondition.ThirdDrawInTurn)
                    {
                        // Only fires for the controller of the trigger card
                        if (drawingPlayer.Id != player.Id) continue;
                        if (drawingPlayer.DrawsThisTurn != 3) continue;
                        _state.Log($"{card.Name} triggers on {drawingPlayer.Name}'s third draw this turn.");
                        _state.StackPush(new TriggeredAbilityStackObject(card, player.Id, trigger.Effect));
                    }
                }
            }
        }
    }

    private void ReturnHandToLibrary(Player player)
    {
        while (player.Hand.Count > 0)
        {
            var card = player.Hand.Cards[0];
            player.Hand.RemoveById(card.Id);
            player.Library.Add(card);
        }
    }
}
