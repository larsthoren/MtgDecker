using MtgDecker.Engine.Enums;

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

    internal void ExecuteAction(GameAction action)
    {
        if (action.PlayerId != _state.Player1.Id && action.PlayerId != _state.Player2.Id)
            throw new InvalidOperationException($"Unknown player ID: {action.PlayerId}");

        var player = action.PlayerId == _state.Player1.Id ? _state.Player1 : _state.Player2;

        switch (action.Type)
        {
            case ActionType.PlayCard:
                var playCard = player.Hand.RemoveById(action.CardId!.Value);
                if (playCard != null)
                {
                    player.Battlefield.Add(playCard);
                    _state.ActionHistory.Push(action);
                    _state.Log($"{player.Name} plays {playCard.Name}.");
                }
                break;

            case ActionType.TapCard:
                var tapTarget = player.Battlefield.Cards.FirstOrDefault(c => c.Id == action.CardId);
                if (tapTarget != null && !tapTarget.IsTapped)
                {
                    tapTarget.IsTapped = true;
                    _state.ActionHistory.Push(action);
                    _state.Log($"{player.Name} taps {tapTarget.Name}.");
                }
                break;

            case ActionType.UntapCard:
                var untapTarget = player.Battlefield.Cards.FirstOrDefault(c => c.Id == action.CardId);
                if (untapTarget != null && untapTarget.IsTapped)
                {
                    untapTarget.IsTapped = false;
                    _state.ActionHistory.Push(action);
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
                    _state.ActionHistory.Push(action);
                    _state.Log($"{player.Name} moves {movedCard.Name} from {action.SourceZone} to {action.DestinationZone}.");
                }
                break;
        }
    }

    public bool UndoLastAction(Guid playerId)
    {
        if (_state.ActionHistory.Count == 0) return false;

        var action = _state.ActionHistory.Peek();
        if (action.PlayerId != playerId) return false;

        _state.ActionHistory.Pop();
        var player = playerId == _state.Player1.Id ? _state.Player1 : _state.Player2;

        switch (action.Type)
        {
            case ActionType.PlayCard:
                var card = player.Battlefield.RemoveById(action.CardId!.Value);
                if (card != null)
                {
                    player.Hand.Add(card);
                    _state.Log($"{player.Name} undoes playing {card.Name}.");
                }
                break;

            case ActionType.TapCard:
                var tapTarget = player.Battlefield.Cards.FirstOrDefault(c => c.Id == action.CardId);
                if (tapTarget != null)
                {
                    tapTarget.IsTapped = false;
                    _state.Log($"{player.Name} undoes tapping {tapTarget.Name}.");
                }
                break;

            case ActionType.UntapCard:
                var untapTarget = player.Battlefield.Cards.FirstOrDefault(c => c.Id == action.CardId);
                if (untapTarget != null)
                {
                    untapTarget.IsTapped = true;
                    _state.Log($"{player.Name} undoes untapping {untapTarget.Name}.");
                }
                break;

            case ActionType.MoveCard:
                var dest = player.GetZone(action.DestinationZone!.Value);
                var src = player.GetZone(action.SourceZone!.Value);
                var movedCard = dest.RemoveById(action.CardId!.Value);
                if (movedCard != null)
                {
                    src.Add(movedCard);
                    _state.Log($"{player.Name} undoes moving {movedCard.Name}.");
                }
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
                ExecuteAction(action);
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
