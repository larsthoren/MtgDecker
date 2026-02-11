using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

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

            if (phase.GrantsPriority)
                await RunPriorityAsync(ct);

            _state.Player1.ManaPool.Clear();
            _state.Player2.ManaPool.Clear();

        } while (_turnStateMachine.AdvancePhase() != null);

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
                    if (player.LandsPlayedThisTurn >= 1)
                    {
                        _state.Log($"{player.Name} cannot play another land this turn.");
                        break;
                    }
                    player.Hand.RemoveById(playCard.Id);
                    player.Battlefield.Add(playCard);
                    player.LandsPlayedThisTurn++;
                    action.IsLandDrop = true;
                    action.DestinationZone = ZoneType.Battlefield;
                    player.ActionHistory.Push(action);
                    _state.Log($"{player.Name} plays {playCard.Name} (land drop).");
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
                        action.DestinationZone = ZoneType.Battlefield;
                        _state.Log($"{player.Name} casts {playCard.Name}.");
                    }
                    action.ManaCostPaid = cost;
                    player.ActionHistory.Push(action);
                }
                else
                {
                    // Part C: Sandbox — no ManaCost, not a land
                    player.Hand.RemoveById(playCard.Id);
                    player.Battlefield.Add(playCard);
                    player.ActionHistory.Push(action);
                    _state.Log($"{player.Name} plays {playCard.Name}.");
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
        }
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
        }

        return true;
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
                    return;

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

    private void DrawCards(Player player, int count)
    {
        for (int i = 0; i < count; i++)
        {
            var card = player.Library.DrawFromTop();
            if (card != null)
                player.Hand.Add(card);
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
