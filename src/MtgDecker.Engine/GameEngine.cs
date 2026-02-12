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

        var player = action.PlayerId == _state.Player1.Id ? _state.Player1 : _state.Player2;

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
                    await ProcessTriggersAsync(GameEvent.EnterBattlefield, playCard, player, ct);
                }
                else if (playCard.ManaCost != null)
                {
                    // Part B: Cast spell with mana payment
                    if (!player.ManaPool.CanPay(playCard.ManaCost))
                    {
                        _state.Log($"{player.Name} cannot cast {playCard.Name} — not enough mana.");
                        break;
                    }

                    var cost = playCard.ManaCost;

                    // Calculate remaining pool after colored requirements
                    var remaining = new Dictionary<ManaColor, int>();
                    foreach (var kvp in player.ManaPool.Available)
                    {
                        var after = kvp.Value;
                        if (cost.ColorRequirements.TryGetValue(kvp.Key, out var needed))
                            after -= needed;
                        if (after > 0)
                            remaining[kvp.Key] = after;
                    }

                    // Deduct colored requirements
                    foreach (var (color, required) in cost.ColorRequirements)
                        player.ManaPool.Deduct(color, required);

                    // Handle generic cost
                    if (cost.GenericCost > 0)
                    {
                        int distinctColors = remaining.Count(kv => kv.Value > 0);
                        int totalRemaining = remaining.Values.Sum();
                        bool useAutoPay = distinctColors <= 1 || totalRemaining == cost.GenericCost;

                        if (!useAutoPay)
                        {
                            // Ambiguous: prompt player
                            var genericPayment = await player.DecisionHandler
                                .ChooseGenericPayment(cost.GenericCost, remaining, ct);

                            // Validate payment: sum must equal generic cost, amounts must not exceed available
                            bool valid = genericPayment.Values.Sum() == cost.GenericCost
                                && genericPayment.All(kv => remaining.TryGetValue(kv.Key, out var avail) && kv.Value <= avail);

                            if (valid)
                            {
                                foreach (var (color, amount) in genericPayment)
                                    player.ManaPool.Deduct(color, amount);
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
                                    player.ManaPool.Deduct(color, take);
                                    toPay -= take;
                                }
                                if (toPay == 0) break;
                            }
                        }
                    }

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
                        await ProcessTriggersAsync(GameEvent.EnterBattlefield, playCard, player, ct);
                    }
                    action.ManaCostPaid = cost;
                    player.ActionHistory.Push(action);
                }
                else
                {
                    // Part C: Sandbox — no ManaCost, not a land
                    player.Hand.RemoveById(playCard.Id);
                    player.Battlefield.Add(playCard);
                    playCard.TurnEnteredBattlefield = _state.TurnNumber;
                    player.ActionHistory.Push(action);
                    _state.Log($"{player.Name} plays {playCard.Name}.");
                    await ProcessTriggersAsync(GameEvent.EnterBattlefield, playCard, player, ct);
                }
                break;

            case ActionType.TapCard:
                var tapTarget = player.Battlefield.Cards.FirstOrDefault(c => c.Id == action.CardId);
                if (tapTarget != null && !tapTarget.IsTapped)
                {
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
                    }
                    else
                    {
                        _state.Log($"{player.Name} taps {tapTarget.Name}.");
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

            case ActionType.MoveCard:
                var source = player.GetZone(action.SourceZone!.Value);
                var dest = player.GetZone(action.DestinationZone!.Value);
                var movedCard = source.RemoveById(action.CardId!.Value);
                if (movedCard != null)
                {
                    dest.Add(movedCard);
                    player.ActionHistory.Push(action);
                    _state.Log($"{player.Name} moves {movedCard.Name} from {action.SourceZone} to {action.DestinationZone}.");
                }
                break;

            case ActionType.CastSpell:
            {
                var castPlayer = action.PlayerId == _state.Player1.Id ? _state.Player1 : _state.Player2;
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

                var pool = castPlayer.ManaPool;
                if (!pool.CanPay(def.ManaCost))
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
                        if (def.TargetFilter.IsLegal(c, ZoneType.Battlefield))
                            eligible.Add(c);
                    foreach (var c in opponent.Battlefield.Cards)
                        if (def.TargetFilter.IsLegal(c, ZoneType.Battlefield))
                            eligible.Add(c);

                    if (eligible.Count == 0)
                    {
                        _state.Log($"No legal targets for {castCard.Name}.");
                        return;
                    }

                    var target = await castPlayer.DecisionHandler.ChooseTarget(
                        castCard.Name, eligible, opponent.Id, ct);
                    targets.Add(target);
                }

                // Calculate remaining pool after colored requirements for generic payment
                var remaining = new Dictionary<ManaColor, int>();
                foreach (var kvp in pool.Available)
                {
                    var after = kvp.Value;
                    if (def.ManaCost.ColorRequirements.TryGetValue(kvp.Key, out var needed))
                        after -= needed;
                    if (after > 0)
                        remaining[kvp.Key] = after;
                }

                // Deduct colored requirements
                var manaPaid = new Dictionary<ManaColor, int>();
                foreach (var (color, amount) in def.ManaCost.ColorRequirements)
                {
                    pool.Deduct(color, amount);
                    manaPaid[color] = amount;
                }

                // Handle generic cost
                if (def.ManaCost.GenericCost > 0)
                {
                    int distinctColors = remaining.Count(kv => kv.Value > 0);
                    int totalRemaining = remaining.Values.Sum();
                    bool useAutoPay = distinctColors <= 1 || totalRemaining == def.ManaCost.GenericCost;

                    if (!useAutoPay)
                    {
                        var genericPayment = await castPlayer.DecisionHandler
                            .ChooseGenericPayment(def.ManaCost.GenericCost, remaining, ct);

                        bool valid = genericPayment.Values.Sum() == def.ManaCost.GenericCost
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
                        var toPay = def.ManaCost.GenericCost;
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

                castPlayer.Hand.RemoveById(castCard.Id);
                var stackObj = new StackObject(castCard, castPlayer.Id, manaPaid, targets, _state.Stack.Count);
                _state.Stack.Add(stackObj);

                action.ManaCostPaid = def.ManaCost;
                castPlayer.ActionHistory.Push(action);

                _state.Log($"{castPlayer.Name} casts {castCard.Name}.");
                break;
            }
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
                if (action.ManaCostPaid != null)
                {
                    foreach (var (color, amount) in action.ManaCostPaid.ColorRequirements)
                        player.ManaPool.Add(color, amount);
                    if (action.ManaCostPaid.GenericCost > 0)
                        player.ManaPool.Add(ManaColor.Colorless, action.ManaCostPaid.GenericCost);
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

            case ActionType.MoveCard:
                var dest = player.GetZone(action.DestinationZone!.Value);
                var movedCard = dest.RemoveById(action.CardId!.Value);
                if (movedCard == null) return false;
                player.ActionHistory.Pop();
                var src = player.GetZone(action.SourceZone!.Value);
                src.Add(movedCard);
                _state.Log($"{player.Name} undoes moving {movedCard.Name}.");
                break;

            case ActionType.CastSpell:
                var stackIdx = _state.Stack.FindLastIndex(s => s.Card.Id == action.CardId);
                if (stackIdx < 0) return false;
                var removedStack = _state.Stack[stackIdx];
                _state.Stack.RemoveAt(stackIdx);
                player.ActionHistory.Pop();
                player.Hand.Add(removedStack.Card);
                foreach (var (color, amount) in removedStack.ManaPaid)
                    player.ManaPool.Add(color, amount);
                _state.Log($"{player.Name} undoes casting {removedStack.Card.Name}.");
                break;
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
                    _state.Combat.DeclareBlocker(blockerId, attackerCardId);
                    var blockerCard = defender.Battlefield.Cards.First(c => c.Id == blockerId);
                    var attackerCard = attacker.Battlefield.Cards.First(c => c.Id == attackerCardId);
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
        ResolveCombatDamage(attacker, defender);

        // Process deaths (state-based actions)
        ProcessCombatDeaths(attacker);
        ProcessCombatDeaths(defender);

        // Check if any player lost due to combat damage
        CheckStateBasedActions();

        // End Combat
        _state.CombatStep = CombatStep.EndCombat;
        _state.Log("End of combat.");

        _state.CombatStep = CombatStep.None;
        _state.Combat = null;
    }

    private void ResolveCombatDamage(Player attacker, Player defender)
    {
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
                    defender.AdjustLife(-damage);
                    _state.Log($"{attackerCard.Name} deals {damage} damage to {defender.Name}. ({defender.Life} life)");
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
    }

    private void ProcessCombatDeaths(Player player)
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
    }

    public void ClearDamage()
    {
        foreach (var card in _state.Player1.Battlefield.Cards)
            card.DamageMarked = 0;
        foreach (var card in _state.Player2.Battlefield.Cards)
            card.DamageMarked = 0;
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
                card.ActiveKeywords.Clear();
            }
            player.MaxLandDrops = 1;
        }

        // Apply effects
        foreach (var effect in _state.ActiveEffects)
        {
            switch (effect.Type)
            {
                case ContinuousEffectType.ModifyPowerToughness:
                    ApplyPowerToughnessEffect(effect, _state.Player1);
                    ApplyPowerToughnessEffect(effect, _state.Player2);
                    break;

                case ContinuousEffectType.GrantKeyword:
                    ApplyKeywordEffect(effect, _state.Player1);
                    ApplyKeywordEffect(effect, _state.Player2);
                    break;

                case ContinuousEffectType.ExtraLandDrop:
                    // Applies to the controller of the source
                    var sourceOwner = _state.Player1.Battlefield.Cards.Any(c => c.Id == effect.SourceId)
                        ? _state.Player1 : _state.Player2;
                    sourceOwner.MaxLandDrops += effect.ExtraLandDrops;
                    break;
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
        foreach (var card in player.Battlefield.Cards)
        {
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

    internal void CheckStateBasedActions()
    {
        if (_state.IsGameOver) return;

        bool p1Dead = _state.Player1.Life <= 0;
        bool p2Dead = _state.Player2.Life <= 0;

        if (p1Dead && p2Dead)
        {
            _state.IsGameOver = true;
            _state.Winner = null; // draw
            _state.Log($"Both players lose — {_state.Player1.Name} ({_state.Player1.Life} life) and {_state.Player2.Name} ({_state.Player2.Life} life).");
        }
        else if (p1Dead)
        {
            _state.IsGameOver = true;
            _state.Winner = _state.Player2.Name;
            _state.Log($"{_state.Player1.Name} loses — life reached {_state.Player1.Life}.");
        }
        else if (p2Dead)
        {
            _state.IsGameOver = true;
            _state.Winner = _state.Player1.Name;
            _state.Log($"{_state.Player2.Name} loses — life reached {_state.Player2.Life}.");
        }
    }

    private async Task ProcessTriggersAsync(GameEvent evt, GameCard source, Player controller, CancellationToken ct)
    {
        if (source.Triggers.Count == 0) return;

        foreach (var trigger in source.Triggers)
        {
            if (trigger.Event != evt) continue;
            if (trigger.Condition == TriggerCondition.Self)
            {
                var ability = new TriggeredAbility(source, controller, trigger);
                _state.Log($"{source.Name} triggers: {trigger.Effect.GetType().Name.Replace("Effect", "")}");
                await ability.ResolveAsync(_state, ct);
            }
        }
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
                        ResolveTopOfStack();
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
                await ExecuteAction(action, ct);
                activePlayerPassed = false;
                nonActivePlayerPassed = false;
                _state.PriorityPlayer = _state.ActivePlayer;
            }
        }
    }

    private void ResolveTopOfStack()
    {
        if (_state.Stack.Count == 0) return;

        var top = _state.Stack[^1];
        _state.Stack.RemoveAt(_state.Stack.Count - 1);
        var controller = top.ControllerId == _state.Player1.Id ? _state.Player1 : _state.Player2;

        _state.Log($"Resolving {top.Card.Name}.");

        if (CardDefinitions.TryGet(top.Card.Name, out var def) && def.Effect != null)
        {
            if (top.Targets.Count > 0)
            {
                var allTargetsLegal = true;
                foreach (var target in top.Targets)
                {
                    var targetOwner = target.PlayerId == _state.Player1.Id ? _state.Player1 : _state.Player2;
                    var targetZone = targetOwner.GetZone(target.Zone);
                    if (!targetZone.Contains(target.CardId))
                    {
                        allTargetsLegal = false;
                        break;
                    }
                }

                if (!allTargetsLegal)
                {
                    _state.Log($"{top.Card.Name} fizzles (illegal target).");
                    controller.Graveyard.Add(top.Card);
                    return;
                }
            }

            def.Effect.Resolve(_state, top);
            controller.Graveyard.Add(top.Card);
        }
        else
        {
            if (top.Card.IsCreature || top.Card.CardTypes.HasFlag(CardType.Enchantment)
                || top.Card.CardTypes.HasFlag(CardType.Artifact))
            {
                top.Card.TurnEnteredBattlefield = _state.TurnNumber;
                controller.Battlefield.Add(top.Card);
            }
            else
            {
                controller.Graveyard.Add(top.Card);
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
