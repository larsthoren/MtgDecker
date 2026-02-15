using MtgDecker.Engine.AI;
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine;

public class GameSession : IDisposable
{
    public string GameId { get; }
    public string? Format { get; set; }
    public bool IsAiOpponent { get; set; }
    public GameState? State { get; private set; }
    public InteractiveDecisionHandler? Player1Handler { get; private set; }
    public InteractiveDecisionHandler? Player2Handler { get; private set; }
    public string? Player1Name { get; private set; }
    public string? Player2Name { get; private set; }
    public bool IsFull => Player1Name != null && Player2Name != null;
    public bool IsStarted { get; private set; }
    public bool IsGameOver => State?.IsGameOver ?? false;
    private string? _surrenderWinner;
    public string? Winner => _surrenderWinner ?? State?.Winner;
    public DateTime LastActivity { get; private set; } = DateTime.UtcNow;
    public event Action? OnStateChanged;

    private List<GameCard>? _player1Deck;
    private List<GameCard>? _player2Deck;
    private GameEngine? _engine;
    private CancellationTokenSource? _cts;
    private readonly object _joinLock = new();
    private readonly object _stateLock = new();

    /// <summary>
    /// True while the game loop is actively executing engine operations and has
    /// not yet yielded to wait for player input. UI operations that mutate
    /// shared state check this flag to avoid corrupting state mid-execution.
    /// The flag is set before engine calls and cleared when the engine yields
    /// (detected via handler IsWaitingForInput), as well as at the end of each
    /// engine call.
    /// </summary>
    private volatile bool _engineBusy;

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
        IPlayerDecisionHandler p2Handler;
        if (IsAiOpponent)
        {
            p2Handler = new AiBotDecisionHandler();
            Player2Handler = null;
        }
        else
        {
            Player2Handler = new InteractiveDecisionHandler();
            p2Handler = Player2Handler;
        }

        var p1 = new Player(Guid.NewGuid(), Player1Name!, Player1Handler);
        var p2 = new Player(Guid.NewGuid(), Player2Name!, p2Handler);

        foreach (var card in _player1Deck!) p1.Library.Add(card);
        foreach (var card in _player2Deck!) p2.Library.Add(card);

        State = new GameState(p1, p2);

        // Coin flip: randomize who goes first
        if (Random.Shared.Next(2) == 1)
        {
            State.ActivePlayer = p2;
            State.PriorityPlayer = p2;
        }
        State.Log($"Coin flip: {State.ActivePlayer.Name} goes first.");

        State.OnStateChanged += () => OnStateChanged?.Invoke();
        _engine = new GameEngine(State);

        IsStarted = true;
        _cts = new CancellationTokenSource();

        _ = Task.Run(() => RunGameLoopAsync(_engine, _cts.Token));
    }

    private async Task RunGameLoopAsync(GameEngine engine, CancellationToken ct)
    {
        LastActivity = DateTime.UtcNow;
        try
        {
            _engineBusy = true;
            try
            {
                await engine.StartGameAsync(ct);
            }
            finally
            {
                _engineBusy = false;
            }
            State!.IsFirstTurn = true;

            while (!State.IsGameOver)
            {
                ct.ThrowIfCancellationRequested();
                _engineBusy = true;
                try
                {
                    await engine.RunTurnAsync(ct);
                }
                finally
                {
                    _engineBusy = false;
                }
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
            _engineBusy = false;
            OnStateChanged?.Invoke();
        }
    }

    /// <summary>
    /// Returns true when the game engine is between actions and safe for UI
    /// mutations. The engine is idle when it has yielded control to an
    /// InteractiveDecisionHandler (waiting for player input). During those
    /// awaits _engineBusy is technically still true (the await hasn't returned),
    /// so we also check whether a handler is waiting for input.
    /// </summary>
    private bool IsEngineSafeForMutation()
    {
        if (!_engineBusy) return true;

        // The engine is in an async method that has yielded to await player
        // input via TaskCompletionSource. It is NOT actively mutating state.
        return (Player1Handler?.IsWaitingForInput ?? false)
            || (Player2Handler?.IsWaitingForInput ?? false);
    }

    public void Surrender(int playerSeat)
    {
        lock (_stateLock)
        {
            LastActivity = DateTime.UtcNow;
            if (State == null) return;
            State.IsGameOver = true;
            _surrenderWinner = playerSeat == 1 ? Player2Name : Player1Name;
            State.Log($"{(playerSeat == 1 ? Player1Name : Player2Name)} surrenders.");
            _cts?.Cancel();
        }
    }

    public bool Undo(int playerSeat)
    {
        lock (_stateLock)
        {
            LastActivity = DateTime.UtcNow;
            if (!IsEngineSafeForMutation() || _engine == null || State == null) return false;
            var playerId = playerSeat == 1 ? State.Player1.Id : State.Player2.Id;
            return _engine.UndoLastAction(playerId);
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
