using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Triggers;

namespace MtgDecker.Engine;

public class GameEngine
{
    private readonly GameState _state;
    private readonly TurnStateMachine _turnStateMachine = new();

    public GameEngine(GameState state)
    {
        _state = state;
    }

    public async Task StartGameAsync(CancellationToken ct = default)
    {
        _state.Player1.Library.Shuffle();
        _state.Player2.Library.Shuffle();

        await RunMulliganAsync(_state.Player1, ct);
        await RunMulliganAsync(_state.Player2, ct);

        _state.Log("Game started.");
    }

    public async Task RunTurnAsync(CancellationToken ct = default)
    {
        _turnStateMachine.Reset();
        _state.ActivePlayer.LandsPlayedThisTurn = 0;
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

        } while (_turnStateMachine.AdvancePhase() != null);

        // Process delayed triggers at end step (e.g., Goblin Pyromancer destroys all Goblins)
        await QueueDelayedTriggersOnStackAsync(GameEvent.EndStep, ct);
        if (_state.Stack.Count > 0)
            await ResolveAllTriggersAsync(ct);

        // Clear end-of-turn effects and recalculate
        StripEndOfTurnEffects();
        RecalculateState();

        // Clear damage at end of turn
        ClearDamage();

        _state.IsFirstTurn = false;
        _state.TurnNumber++;
        _state.ActivePlayer = _state.GetOpponent(_state.ActivePlayer);
    }

    internal void ExecuteTurnBasedAction(Phase phase)
    {
        switch (phase)
        {
            case Phase.Untap:
                foreach (var card in _state.ActivePlayer.Battlefield.Cards)
                    card.IsTapped = false;
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

                var drawn = _state.ActivePlayer.Library.DrawFromTop();
                if (drawn != null)
                {
                    _state.ActivePlayer.Hand.Add(drawn);
                    _state.Log($"{_state.ActivePlayer.Name} draws a card.");
                }
                else
                {
                    var loser = _state.ActivePlayer;
                    var winner = _state.GetOpponent(loser);
                    _state.IsGameOver = true;
                    _state.Winner = winner.Name;
                    _state.Log($"{loser.Name} loses — cannot draw from an empty library.");
                }
                break;
        }
    }

    internal async Task ExecuteAction(GameAction action, CancellationToken ct = default)
    {
        if (action.PlayerId != _state.Player1.Id && action.PlayerId != _state.Player2.Id)
            throw new InvalidOperationException($"Unknown player ID: {action.PlayerId}");

        var player = _state.GetPlayer(action.PlayerId);

        switch (action.Type)
        {
            case ActionType.PlayCard:
                var playCard = player.Hand.Cards.FirstOrDefault(c => c.Id == action.CardId);
                if (playCard == null) break;

                if (playCard.IsLand)
                {
                    // Part A: Land drop enforcement
                    if (player.LandsPlayedThisTurn >= player.MaxLandDrops)
                    {
                        _state.Log($"{player.Name} cannot play another land this turn.");
                        break;
                    }
                    player.Hand.RemoveById(playCard.Id);
                    player.Battlefield.Add(playCard);
                    playCard.TurnEnteredBattlefield = _state.TurnNumber;
                    player.LandsPlayedThisTurn++;
                    action.IsLandDrop = true;
                    action.DestinationZone = ZoneType.Battlefield;
                    player.ActionHistory.Push(action);
                    _state.Log($"{player.Name} plays {playCard.Name} (land drop).");
                    await QueueSelfTriggersOnStackAsync(GameEvent.EnterBattlefield, playCard, player, ct);
                    await OnBoardChangedAsync(ct);
                }
                else if (playCard.ManaCost != null)
                {
                    // Part B: Cast spell with mana payment
                    // Apply cost modification from continuous effects
                    var effectiveCost = playCard.ManaCost;
                    var costReduction = ComputeCostModification(playCard, player);
                    if (costReduction != 0)
                        effectiveCost = effectiveCost.WithGenericReduction(-costReduction);

                    if (!player.ManaPool.CanPay(effectiveCost))
                    {
                        _state.Log($"{player.Name} cannot cast {playCard.Name} — not enough mana.");
                        break;
                    }

                    var playManaPaid = await PayManaCostAsync(effectiveCost, player, ct);

                    // Move card to destination
                    player.Hand.RemoveById(playCard.Id);
                    bool isInstantOrSorcery = playCard.CardTypes.HasFlag(CardType.Instant)
                                            || playCard.CardTypes.HasFlag(CardType.Sorcery);
                    if (isInstantOrSorcery)
                    {
                        player.Graveyard.Add(playCard);
                        action.DestinationZone = ZoneType.Graveyard;
                        _state.Log($"{player.Name} casts {playCard.Name} (→ graveyard).");
                    }
                    else
                    {
                        player.Battlefield.Add(playCard);
                        playCard.TurnEnteredBattlefield = _state.TurnNumber;
                        action.DestinationZone = ZoneType.Battlefield;
                        _state.Log($"{player.Name} casts {playCard.Name}.");

                        // Aura attachment: prompt for target after entering battlefield
                        await TryAttachAuraAsync(playCard, player, ct);

                        await QueueSelfTriggersOnStackAsync(GameEvent.EnterBattlefield, playCard, player, ct);
                        await OnBoardChangedAsync(ct);
                    }
                    // Fire SpellCast board triggers (e.g., enchantress draw on enchantment cast)
                    await QueueBoardTriggersOnStackAsync(GameEvent.SpellCast, playCard, ct);
                    action.ManaCostPaid = effectiveCost;
                    action.ActualManaPaid = playManaPaid;
                    player.ActionHistory.Push(action);
                }
                else
                {
                    // No ManaCost, not a land — card not supported in engine
                    _state.Log($"{playCard.Name} is not supported in the engine (no card definition).");
                    break;
                }
                break;

            case ActionType.TapCard:
                var tapTarget = player.Battlefield.Cards.FirstOrDefault(c => c.Id == action.CardId);
                if (tapTarget != null && !tapTarget.IsTapped)
                {
                    // Summoning sickness: creatures that entered this turn can't be tapped (lands/artifacts exempt)
                    if (tapTarget.IsCreature && tapTarget.HasSummoningSickness(_state.TurnNumber))
                    {
                        _state.Log($"{tapTarget.Name} has summoning sickness.");
                        break;
                    }

                    tapTarget.IsTapped = true;
                    player.ActionHistory.Push(action);

                    if (tapTarget.ManaAbility != null)
                    {
                        var ability = tapTarget.ManaAbility;
                        if (ability.Type == ManaAbilityType.Fixed)
                        {
                            player.ManaPool.Add(ability.FixedColor!.Value);
                            action.ManaProduced = ability.FixedColor!.Value;
                            _state.Log($"{player.Name} taps {tapTarget.Name} for {ability.FixedColor}.");
                        }
                        else if (ability.Type == ManaAbilityType.Choice)
                        {
                            var chosen = await player.DecisionHandler.ChooseManaColor(
                                ability.ChoiceColors!, ct);
                            player.ManaPool.Add(chosen);
                            action.ManaProduced = chosen;
                            _state.Log($"{player.Name} taps {tapTarget.Name} for {chosen}.");
                        }
                        else if (ability.Type == ManaAbilityType.Dynamic)
                        {
                            var amount = ability.CountFunc!(player);
                            if (amount > 0)
                            {
                                player.ManaPool.Add(ability.DynamicColor!.Value, amount);
                                _state.Log($"{player.Name} taps {tapTarget.Name} for {amount} {ability.DynamicColor}.");
                            }
                            else
                            {
                                _state.Log($"{player.Name} taps {tapTarget.Name} (produces no mana).");
                            }
                        }
                    }
                    else
                    {
                        _state.Log($"{player.Name} taps {tapTarget.Name}.");
                    }

                    // Fire mana triggers from auras attached to this permanent (immediate — mana abilities don't use stack)
                    foreach (var aura in player.Battlefield.Cards.Where(c => c.AttachedTo == tapTarget.Id).ToList())
                    {
                        var auraTriggers = aura.Triggers.Count > 0
                            ? aura.Triggers
                            : (CardDefinitions.TryGet(aura.Name, out var auraDef2) ? auraDef2.Triggers : []);

                        foreach (var trigger in auraTriggers)
                        {
                            if (trigger.Condition == TriggerCondition.AttachedPermanentTapped)
                            {
                                var ctx = new EffectContext(_state, player, aura, player.DecisionHandler)
                                {
                                    FireLeaveBattlefieldTriggers = async card =>
                                    {
                                        var ctrl = _state.Player1.Battlefield.Contains(card.Id) ? _state.Player1
                                            : _state.Player2.Battlefield.Contains(card.Id) ? _state.Player2 : null;
                                        if (ctrl != null) await FireLeaveBattlefieldTriggersAsync(card, ctrl, ct);
                                    },
                                };
                                await trigger.Effect.Execute(ctx);
                            }
                        }
                    }
                }
                break;

            case ActionType.UntapCard:
                var untapTarget = player.Battlefield.Cards.FirstOrDefault(c => c.Id == action.CardId);
                if (untapTarget != null && untapTarget.IsTapped)
                {
                    untapTarget.IsTapped = false;
                    player.ActionHistory.Push(action);
                    _state.Log($"{player.Name} untaps {untapTarget.Name}.");
                }
                break;

            case ActionType.ActivateFetch:
            {
                var fetchLand = player.Battlefield.Cards.FirstOrDefault(c => c.Id == action.CardId);
                if (fetchLand == null || fetchLand.IsTapped) break;

                var fetchDef = CardDefinitions.TryGet(fetchLand.Name, out var fd) ? fd : null;
                var fetchAbility = fetchDef?.FetchAbility ?? fetchLand.FetchAbility;
                if (fetchAbility == null) break;

                // Pay costs: 1 life + sacrifice
                player.AdjustLife(-1);
                await FireLeaveBattlefieldTriggersAsync(fetchLand, player, ct);
                player.Battlefield.RemoveById(fetchLand.Id);
                player.Graveyard.Add(fetchLand);
                _state.Log($"{player.Name} sacrifices {fetchLand.Name}, pays 1 life ({player.Life}).");

                // Search library for matching land
                var searchTypes = fetchAbility.SearchTypes;
                var eligible = player.Library.Cards
                    .Where(c => c.IsLand && searchTypes.Any(t =>
                        c.Subtypes.Contains(t) || c.Name.Equals(t, StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                if (eligible.Count > 0)
                {
                    var chosenId = await player.DecisionHandler.ChooseCard(
                        eligible, $"Search for a land ({string.Join(" or ", searchTypes)})",
                        optional: true, ct);

                    if (chosenId != null)
                    {
                        var land = player.Library.RemoveById(chosenId.Value);
                        if (land != null)
                        {
                            player.Battlefield.Add(land);
                            land.TurnEnteredBattlefield = _state.TurnNumber;
                            _state.Log($"{player.Name} fetches {land.Name}.");
                            await QueueSelfTriggersOnStackAsync(GameEvent.EnterBattlefield, land, player, ct);
                            await OnBoardChangedAsync(ct);
                        }
                    }
                }
                else
                {
                    _state.Log($"{player.Name} finds no matching land.");
                }

                player.Library.Shuffle();
                player.ActionHistory.Push(action);
                break;
            }

            case ActionType.CastSpell:
            {
                var castPlayer = _state.GetPlayer(action.PlayerId);
                var castCard = castPlayer.Hand.Cards.FirstOrDefault(c => c.Id == action.CardId);
                if (castCard == null)
                {
                    _state.Log("Card not found in hand.");
                    return;
                }

                if (!CardDefinitions.TryGet(castCard.Name, out var def) || def.ManaCost == null)
                {
                    _state.Log($"Cannot cast {castCard.Name} — no registered mana cost.");
                    return;
                }

                bool isInstant = def.CardTypes.HasFlag(CardType.Instant);
                if (!isInstant && !CanCastSorcery(castPlayer.Id))
                {
                    _state.Log($"Cannot cast {castCard.Name} at this time (sorcery-speed only).");
                    return;
                }

                // Apply cost modification from continuous effects
                var castEffectiveCost = def.ManaCost;
                var castCostReduction = ComputeCostModification(castCard, castPlayer);
                if (castCostReduction != 0)
                    castEffectiveCost = castEffectiveCost.WithGenericReduction(-castCostReduction);

                if (!castPlayer.ManaPool.CanPay(castEffectiveCost))
                {
                    _state.Log($"Not enough mana to cast {castCard.Name}.");
                    return;
                }

                var targets = new List<TargetInfo>();
                if (def.TargetFilter != null)
                {
                    var eligible = new List<GameCard>();
                    var opponent = _state.GetOpponent(castPlayer);
                    foreach (var c in castPlayer.Battlefield.Cards)
                        if (def.TargetFilter.IsLegal(c, ZoneType.Battlefield) && !HasShroud(c))
                            eligible.Add(c);
                    foreach (var c in opponent.Battlefield.Cards)
                        if (def.TargetFilter.IsLegal(c, ZoneType.Battlefield) && !HasShroud(c))
                            eligible.Add(c);

                    // Add player sentinels if the filter allows player targets
                    var dummyCard = new GameCard { Name = "Player" };
                    if (def.TargetFilter.IsLegal(dummyCard, ZoneType.None))
                    {
                        eligible.Add(new GameCard { Id = Guid.Empty, Name = castPlayer.Name });
                        eligible.Add(new GameCard { Id = Guid.Empty, Name = opponent.Name });
                    }

                    // Add stack objects as targets if the filter allows spell targets
                    if (def.TargetFilter.IsLegal(dummyCard, ZoneType.Stack))
                    {
                        foreach (var so in _state.Stack.OfType<StackObject>())
                        {
                            if (def.TargetFilter.IsLegal(so.Card, ZoneType.Stack))
                                eligible.Add(so.Card);
                        }
                    }

                    if (eligible.Count == 0)
                    {
                        _state.Log($"No legal targets for {castCard.Name}.");
                        return;
                    }

                    var target = await castPlayer.DecisionHandler.ChooseTarget(
                        castCard.Name, eligible, opponent.Id, ct);

                    // Convert player sentinel targets to proper TargetInfo
                    if (target.Zone == ZoneType.None)
                    {
                        // Player target — ensure correct convention
                        targets.Add(new TargetInfo(Guid.Empty, target.PlayerId, ZoneType.None));
                    }
                    else
                    {
                        // Auto-detect stack targets: if the chosen card is on the stack, use ZoneType.Stack
                        var stackTarget = _state.Stack.OfType<StackObject>().FirstOrDefault(s => s.Card.Id == target.CardId);
                        if (stackTarget != null)
                            targets.Add(new TargetInfo(stackTarget.Card.Id, stackTarget.ControllerId, ZoneType.Stack));
                        else
                            targets.Add(target);
                    }
                }

                var manaPaid = await PayManaCostAsync(castEffectiveCost, castPlayer, ct);

                castPlayer.Hand.RemoveById(castCard.Id);
                var stackObj = new StackObject(castCard, castPlayer.Id, manaPaid, targets, _state.Stack.Count);
                _state.Stack.Add(stackObj);

                action.ManaCostPaid = castEffectiveCost;
                castPlayer.ActionHistory.Push(action);

                _state.Log($"{castPlayer.Name} casts {castCard.Name}.");

                // Fire SpellCast board triggers (e.g., enchantress draw on enchantment cast)
                await QueueBoardTriggersOnStackAsync(GameEvent.SpellCast, castCard, ct);
                break;
            }

            case ActionType.ActivateAbility:
            {
                var abilitySource = player.Battlefield.Cards.FirstOrDefault(c => c.Id == action.CardId);
                if (abilitySource == null) break;

                if (!CardDefinitions.TryGet(abilitySource.Name, out var abilityDef) || abilityDef.ActivatedAbility == null)
                {
                    _state.Log($"{abilitySource.Name} has no activated ability.");
                    break;
                }

                var ability = abilityDef.ActivatedAbility;
                var cost = ability.Cost;

                // Validate: tap cost when already tapped
                if (cost.TapSelf && abilitySource.IsTapped)
                {
                    _state.Log($"Cannot activate {abilitySource.Name} — already tapped.");
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

                // Pay costs: mana
                if (cost.ManaCost != null)
                    await PayManaCostAsync(cost.ManaCost, player, ct);

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

                // Pay costs: remove counter
                if (cost.RemoveCounterType.HasValue)
                {
                    abilitySource.RemoveCounter(cost.RemoveCounterType.Value);
                }

                // Find effect target
                GameCard? effectTarget = null;
                if (action.TargetCardId.HasValue)
                {
                    effectTarget = _state.Player1.Battlefield.Cards.FirstOrDefault(c => c.Id == action.TargetCardId.Value)
                                ?? _state.Player2.Battlefield.Cards.FirstOrDefault(c => c.Id == action.TargetCardId.Value);
                }

                // Shroud check: cannot target a permanent with shroud
                if (effectTarget != null && HasShroud(effectTarget))
                {
                    _state.Log($"{effectTarget.Name} has shroud — cannot be targeted.");
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

                // Build context and execute effect
                var effectContext = new EffectContext(_state, player, abilitySource, player.DecisionHandler)
                {
                    Target = effectTarget,
                    TargetPlayerId = action.TargetPlayerId,
                    FireLeaveBattlefieldTriggers = async card =>
                    {
                        var ctrl = _state.Player1.Battlefield.Contains(card.Id) ? _state.Player1
                            : _state.Player2.Battlefield.Contains(card.Id) ? _state.Player2 : null;
                        if (ctrl != null) await FireLeaveBattlefieldTriggersAsync(card, ctrl, ct);
                    },
                };

                await ability.Effect.Execute(effectContext, ct);
                await OnBoardChangedAsync(ct);

                player.ActionHistory.Push(action);
                break;
            }

            case ActionType.Cycle:
            {
                var cycleCard = player.Hand.Cards.FirstOrDefault(c => c.Id == action.CardId);
                if (cycleCard == null) break;

                if (!CardDefinitions.TryGet(cycleCard.Name, out var cycleDef) || cycleDef.CyclingCost == null)
                {
                    _state.Log($"{cycleCard.Name} cannot be cycled.");
                    break;
                }

                var cyclingCost = cycleDef.CyclingCost;
                if (!player.ManaPool.CanPay(cyclingCost))
                {
                    _state.Log($"Cannot cycle {cycleCard.Name} — not enough mana.");
                    break;
                }

                // Pay mana using ManaPool.Pay (handles colored + generic)
                player.ManaPool.Pay(cyclingCost);

                // Discard to graveyard
                player.Hand.RemoveById(cycleCard.Id);
                player.Graveyard.Add(cycleCard);

                // Draw a card
                DrawCards(player, 1);
                _state.Log($"{player.Name} cycles {cycleCard.Name}.");

                // Queue cycling triggers on stack
                foreach (var trigger in cycleDef.CyclingTriggers)
                {
                    _state.Log($"{cycleCard.Name} triggers: {trigger.Effect.GetType().Name.Replace("Effect", "")}");
                    _state.Stack.Add(new TriggeredAbilityStackObject(cycleCard, player.Id, trigger.Effect));
                }

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

    private bool HasShroud(GameCard card) => card.ActiveKeywords.Contains(Keyword.Shroud);

    private bool HasPlayerShroud(Guid playerId)
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

    private async Task TryAttachAuraAsync(GameCard playCard, Player player, CancellationToken ct)
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
            .Where(c => !HasShroud(c))
            .ToList();

        if (eligible.Count > 0)
        {
            var chosenId = await player.DecisionHandler.ChooseCard(
                eligible, $"Choose a target for {playCard.Name}", optional: false, ct);
            if (chosenId.HasValue)
                playCard.AttachedTo = chosenId.Value;
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
            && _state.Stack.Count == 0;
    }

    public bool UndoLastAction(Guid playerId)
    {
        var player = playerId == _state.Player1.Id ? _state.Player1 : _state.Player2;

        if (player.ActionHistory.Count == 0) return false;

        var action = player.ActionHistory.Peek();

        switch (action.Type)
        {
            case ActionType.PlayCard:
                var destZone = action.DestinationZone == ZoneType.Graveyard
                    ? player.Graveyard : player.Battlefield;
                var card = destZone.RemoveById(action.CardId!.Value);
                if (card == null) return false;
                player.ActionHistory.Pop();
                player.Hand.Add(card);
                if (action.IsLandDrop)
                    player.LandsPlayedThisTurn--;
                if (action.ActualManaPaid != null)
                {
                    foreach (var (color, amount) in action.ActualManaPaid)
                        player.ManaPool.Add(color, amount);
                }
                _state.Log($"{player.Name} undoes playing {card.Name}.");
                break;

            case ActionType.TapCard:
                var tapTarget = player.Battlefield.Cards.FirstOrDefault(c => c.Id == action.CardId);
                if (tapTarget == null) return false;
                player.ActionHistory.Pop();
                tapTarget.IsTapped = false;
                if (action.ManaProduced.HasValue)
                    player.ManaPool.Deduct(action.ManaProduced.Value, 1);
                _state.Log($"{player.Name} undoes tapping {tapTarget.Name}.");
                break;

            case ActionType.UntapCard:
                var untapTarget = player.Battlefield.Cards.FirstOrDefault(c => c.Id == action.CardId);
                if (untapTarget == null) return false;
                player.ActionHistory.Pop();
                untapTarget.IsTapped = true;
                _state.Log($"{player.Name} undoes untapping {untapTarget.Name}.");
                break;

            case ActionType.CastSpell:
                var stackIdx = _state.Stack.FindLastIndex(s => s is StackObject so && so.Card.Id == action.CardId);
                if (stackIdx < 0) return false;
                var removedStack = (StackObject)_state.Stack[stackIdx];
                _state.Stack.RemoveAt(stackIdx);
                player.ActionHistory.Pop();
                player.Hand.Add(removedStack.Card);
                foreach (var (color, amount) in removedStack.ManaPaid)
                    player.ManaPool.Add(color, amount);
                _state.Log($"{player.Name} undoes casting {removedStack.Card.Name}.");
                break;

            case ActionType.Cycle:
                _state.Log("Cycling cannot be undone.");
                return false;
        }

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

        // Declare Attackers
        _state.CombatStep = CombatStep.DeclareAttackers;

        var eligibleAttackers = attacker.Battlefield.Cards
            .Where(c => c.IsCreature && !c.IsTapped && !c.HasSummoningSickness(_state.TurnNumber))
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

        // Fire SelfAttacks triggers (e.g., Piledriver pump) after attackers declared
        foreach (var attackerId in validAttackerIds)
        {
            var card = attacker.Battlefield.Cards.FirstOrDefault(c => c.Id == attackerId);
            if (card != null)
                await QueueAttackTriggersOnStackAsync(card, ct);
        }

        // Priority round after attack triggers
        if (_state.Stack.Count > 0)
            await RunPriorityAsync(ct);

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
                    var attackerCard = attacker.Battlefield.Cards.First(c => c.Id == attackerCardId);

                    // Mountainwalk: cannot be blocked if defender controls a Mountain
                    if (attackerCard.ActiveKeywords.Contains(Keyword.Mountainwalk)
                        && defender.Battlefield.Cards.Any(c => c.Subtypes.Contains("Mountain")))
                    {
                        _state.Log($"{attackerCard.Name} has mountainwalk — cannot be blocked.");
                        continue;
                    }

                    _state.Combat.DeclareBlocker(blockerId, attackerCardId);
                    var blockerCard = defender.Battlefield.Cards.First(c => c.Id == blockerId);
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
                    .Select(id => defender.Battlefield.Cards.First(c => c.Id == id))
                    .ToList();

                var orderedIds = await attacker.DecisionHandler.OrderBlockers(attackerId, blockerCards, ct);
                _state.Combat.SetBlockerOrder(attackerId, orderedIds.ToList());
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
        if (_state.Stack.Count > 0)
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
        if (_state.Stack.Count > 0)
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
                // Unblocked: deal damage to defending player
                var damage = attackerCard.Power ?? 0;
                if (damage > 0)
                {
                    if (HasPlayerDamageProtection(defender.Id))
                    {
                        _state.Log($"{attackerCard.Name}'s {damage} damage to {defender.Name} is prevented (protection).");
                    }
                    else
                    {
                        defender.AdjustLife(-damage);
                        _state.Log($"{attackerCard.Name} deals {damage} damage to {defender.Name}. ({defender.Life} life)");
                        unblockedAttackers.Add(attackerCard);
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

    public void RecalculateState()
    {
        // Preserve temporary (UntilEndOfTurn) effects before rebuild
        var tempEffects = _state.ActiveEffects.Where(e => e.UntilEndOfTurn).ToList();

        // Rebuild ActiveEffects from CardDefinitions on the battlefield
        _state.ActiveEffects.Clear();
        RebuildActiveEffects(_state.Player1);
        RebuildActiveEffects(_state.Player2);

        // Re-add temporary effects
        _state.ActiveEffects.AddRange(tempEffects);

        // Reset all effective values for both players
        foreach (var player in new[] { _state.Player1, _state.Player2 })
        {
            foreach (var card in player.Battlefield.Cards)
            {
                card.EffectivePower = null;
                card.EffectiveToughness = null;
                card.EffectiveCardTypes = null;
                card.ActiveKeywords.Clear();
            }
            player.MaxLandDrops = 1;
        }

        // Layer 1: Type-changing effects (BecomeCreature)
        foreach (var effect in _state.ActiveEffects.Where(e => e.Type == ContinuousEffectType.BecomeCreature))
        {
            ApplyBecomeCreatureEffect(effect, _state.Player1);
            ApplyBecomeCreatureEffect(effect, _state.Player2);
        }

        // Layer 2: P/T modification
        foreach (var effect in _state.ActiveEffects.Where(e => e.Type == ContinuousEffectType.ModifyPowerToughness))
        {
            ApplyPowerToughnessEffect(effect, _state.Player1);
            ApplyPowerToughnessEffect(effect, _state.Player2);
        }

        // Layer 3: Keywords
        foreach (var effect in _state.ActiveEffects.Where(e => e.Type == ContinuousEffectType.GrantKeyword))
        {
            ApplyKeywordEffect(effect, _state.Player1);
            ApplyKeywordEffect(effect, _state.Player2);
        }

        // Non-layered effects
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
            if (card.Id == effect.SourceId) continue; // "each other" exclusion
            if (!effect.Applies(card, player)) continue;

            // Add Creature type
            card.EffectiveCardTypes = (card.EffectiveCardTypes ?? card.CardTypes) | CardType.Creature;

            // Set P/T to CMC
            if (effect.SetPowerToughnessToCMC && card.ManaCost != null)
            {
                var cmc = card.ManaCost.ConvertedManaCost;
                card.EffectivePower = cmc;
                card.EffectiveToughness = cmc;
            }
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
                var effect = templateEffect with { SourceId = card.Id };
                _state.ActiveEffects.Add(effect);
            }
        }
    }

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
            var chosenId = await player.DecisionHandler.ChooseCard(
                duplicates,
                $"Choose which {duplicates[0].Name} to keep (legendary rule)",
                optional: false, ct);

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

    /// <summary>Queues Self triggers for a specific card onto the stack.</summary>
    internal Task QueueSelfTriggersOnStackAsync(GameEvent evt, GameCard source, Player controller, CancellationToken ct = default)
    {
        if (source.Triggers.Count == 0) return Task.CompletedTask;

        foreach (var trigger in source.Triggers)
        {
            if (trigger.Event != evt) continue;
            if (trigger.Condition != TriggerCondition.Self) continue;

            _state.Log($"{source.Name} triggers: {trigger.Effect.GetType().Name.Replace("Effect", "")}");
            _state.Stack.Add(new TriggeredAbilityStackObject(source, controller.Id, trigger.Effect));
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
            _state.Stack.Add(t);
        // Non-active player's triggers on top (resolve first via LIFO)
        foreach (var t in nonActiveTriggers)
            _state.Stack.Add(t);

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
                    TriggerCondition.SelfAttacks => false,
                    _ => false,
                };

                if (matches)
                {
                    _state.Log($"{permanent.Name} triggers: {trigger.Effect.GetType().Name.Replace("Effect", "")}");
                    result.Add(new TriggeredAbilityStackObject(permanent, player.Id, trigger.Effect));
                }
            }
        }

        return result;
    }

    /// <summary>Queues attack triggers onto the stack.</summary>
    internal Task QueueAttackTriggersOnStackAsync(GameCard attacker, CancellationToken ct = default)
    {
        var player = _state.ActivePlayer;
        var triggers = attacker.Triggers.Count > 0
            ? attacker.Triggers
            : (CardDefinitions.TryGet(attacker.Name, out var def) ? def.Triggers : []);

        foreach (var trigger in triggers)
        {
            if (trigger.Condition != TriggerCondition.SelfAttacks) continue;

            _state.Log($"{attacker.Name} triggers: {trigger.Effect.GetType().Name.Replace("Effect", "")}");
            _state.Stack.Add(new TriggeredAbilityStackObject(attacker, player.Id, trigger.Effect));
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
            _state.Stack.Add(new TriggeredAbilityStackObject(source, controller.Id, delayed.Effect));
            _state.DelayedTriggers.Remove(delayed);
        }

        return Task.CompletedTask;
    }

    internal Task FireLeaveBattlefieldTriggersAsync(GameCard card, Player controller, CancellationToken ct)
    {
        if (!CardDefinitions.TryGet(card.Name, out var def)) return Task.CompletedTask;

        foreach (var trigger in def.Triggers)
        {
            if (trigger.Condition == TriggerCondition.SelfLeavesBattlefield)
            {
                _state.Log($"{card.Name} triggers: {trigger.Effect.GetType().Name.Replace("Effect", "")}");
                _state.Stack.Add(new TriggeredAbilityStackObject(card, controller.Id, trigger.Effect));
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>Resolves all items on the stack (for testing).</summary>
    internal async Task ResolveAllTriggersAsync(CancellationToken ct = default)
    {
        while (_state.Stack.Count > 0)
            await ResolveTopOfStackAsync(ct);
    }

    internal async Task RunPriorityAsync(CancellationToken ct = default)
    {
        _state.PriorityPlayer = _state.ActivePlayer;
        bool activePlayerPassed = false;
        bool nonActivePlayerPassed = false;

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
                    if (_state.Stack.Count > 0)
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
                    activePlayerPassed = false;
                    nonActivePlayerPassed = false;
                    _state.PriorityPlayer = _state.ActivePlayer;
                }
                else
                {
                    // Action was rejected by the engine — treat as pass to avoid infinite loops
                    if (_state.PriorityPlayer == _state.ActivePlayer)
                        activePlayerPassed = true;
                    else
                        nonActivePlayerPassed = true;

                    _state.PriorityPlayer = _state.GetOpponent(_state.PriorityPlayer);
                }
            }
        }
    }

    private async Task ResolveTopOfStackAsync(CancellationToken ct = default)
    {
        if (_state.Stack.Count == 0) return;

        var top = _state.Stack[^1];
        _state.Stack.RemoveAt(_state.Stack.Count - 1);
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
            await triggered.Effect.Execute(context, ct);
            await OnBoardChangedAsync(ct);
            return;
        }

        if (top is StackObject spell)
        {
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
                        controller.Graveyard.Add(spell.Card);
                        return;
                    }
                }

                await def.Effect.ResolveAsync(_state, spell, controller.DecisionHandler, ct);
                controller.Graveyard.Add(spell.Card);
                await OnBoardChangedAsync(ct);
            }
            else
            {
                if (spell.Card.IsCreature || spell.Card.CardTypes.HasFlag(CardType.Enchantment)
                    || spell.Card.CardTypes.HasFlag(CardType.Artifact))
                {
                    spell.Card.TurnEnteredBattlefield = _state.TurnNumber;
                    controller.Battlefield.Add(spell.Card);

                    // Aura attachment on stack resolution
                    await TryAttachAuraAsync(spell.Card, controller, ct);

                    await QueueSelfTriggersOnStackAsync(GameEvent.EnterBattlefield, spell.Card, controller, ct);
                    await OnBoardChangedAsync(ct);
                }
                else
                {
                    controller.Graveyard.Add(spell.Card);
                }
            }
        }
    }

    internal async Task RunMulliganAsync(Player player, CancellationToken ct = default)
    {
        int mulliganCount = 0;

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

    internal void DrawCards(Player player, int count)
    {
        for (int i = 0; i < count; i++)
        {
            var card = player.Library.DrawFromTop();
            if (card != null)
            {
                player.Hand.Add(card);
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
