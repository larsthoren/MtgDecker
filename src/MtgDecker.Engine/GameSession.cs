using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine;

public class GameSession : IDisposable
{
    public string GameId { get; }
    public GameState? State { get; private set; }
    public InteractiveDecisionHandler? Player1Handler { get; private set; }
    public InteractiveDecisionHandler? Player2Handler { get; private set; }
    public string? Player1Name { get; private set; }
    public string? Player2Name { get; private set; }
    public bool IsFull => Player1Name != null && Player2Name != null;
    public bool IsStarted { get; private set; }
    public bool IsGameOver => State?.IsGameOver ?? false;
    public string? Winner { get; private set; }
    public event Action? OnStateChanged;

    private List<GameCard>? _player1Deck;
    private List<GameCard>? _player2Deck;
    private GameEngine? _engine;
    private CancellationTokenSource? _cts;
    private readonly object _joinLock = new();
    private readonly object _stateLock = new();

    public GameSession(string gameId)
    {
        GameId = gameId;
    }

    public int JoinPlayer(string playerName, List<GameCard> deck)
    {
        lock (_joinLock)
        {
            if (Player1Name == null)
            {
                Player1Name = playerName;
                _player1Deck = deck;
                return 1;
            }
            if (Player2Name == null)
            {
                Player2Name = playerName;
                _player2Deck = deck;
                return 2;
            }
            throw new InvalidOperationException("Game is full.");
        }
    }

    public async Task StartAsync()
    {
        if (!IsFull)
            throw new InvalidOperationException("Need two players to start.");

        Player1Handler = new InteractiveDecisionHandler();
        Player2Handler = new InteractiveDecisionHandler();

        var p1 = new Player(Guid.NewGuid(), Player1Name!, Player1Handler);
        var p2 = new Player(Guid.NewGuid(), Player2Name!, Player2Handler);

        foreach (var card in _player1Deck!) p1.Library.Add(card);
        foreach (var card in _player2Deck!) p2.Library.Add(card);

        State = new GameState(p1, p2);
        State.OnStateChanged += () => OnStateChanged?.Invoke();
        _engine = new GameEngine(State);

        IsStarted = true;
        _cts = new CancellationTokenSource();

        _ = Task.Run(() => RunGameLoopAsync(_engine, _cts.Token));
    }

    private async Task RunGameLoopAsync(GameEngine engine, CancellationToken ct)
    {
        try
        {
            await engine.StartGameAsync(ct);
            State!.IsFirstTurn = true;

            while (!State.IsGameOver)
            {
                ct.ThrowIfCancellationRequested();
                await engine.RunTurnAsync(ct);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            State!.IsGameOver = true;
            State.Log($"Game ended due to an unexpected error: {ex.Message}");
        }
        finally
        {
            OnStateChanged?.Invoke();
        }
    }

    public void Surrender(int playerSeat)
    {
        lock (_stateLock)
        {
            if (State == null) return;
            State.IsGameOver = true;
            Winner = playerSeat == 1 ? Player2Name : Player1Name;
            State.Log($"{(playerSeat == 1 ? Player1Name : Player2Name)} surrenders.");
            _cts?.Cancel();
        }
    }

    public bool Undo(int playerSeat)
    {
        lock (_stateLock)
        {
            if (_engine == null || State == null) return false;
            var playerId = playerSeat == 1 ? State.Player1.Id : State.Player2.Id;
            return _engine.UndoLastAction(playerId);
        }
    }

    public void AdjustLife(int playerSeat, int delta)
    {
        lock (_stateLock)
        {
            if (State == null) return;
            var player = playerSeat == 1 ? State.Player1 : State.Player2;
            var oldLife = player.Life;
            player.AdjustLife(delta);
            State.Log($"{player.Name}'s life: {oldLife} \u2192 {player.Life}");

            if (player.Life <= 0)
            {
                State.IsGameOver = true;
                Winner = State.GetOpponent(player).Name;
                State.Log($"{player.Name} loses \u2014 life reached {player.Life}.");
                _cts?.Cancel();
            }
        }
    }

    public void DrawCard(int playerSeat)
    {
        lock (_stateLock)
        {
            if (State == null) return;
            var player = playerSeat == 1 ? State.Player1 : State.Player2;
            var card = player.Library.DrawFromTop();
            if (card != null)
            {
                player.Hand.Add(card);
                State.Log($"{player.Name} draws a card.");
            }
        }
    }

    public InteractiveDecisionHandler? GetHandler(int playerSeat) =>
        playerSeat == 1 ? Player1Handler : Player2Handler;

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
