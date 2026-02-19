using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine;

public class GameState
{
    public Player Player1 { get; }
    public Player Player2 { get; }
    public Player ActivePlayer { get; set; }
    public Player PriorityPlayer { get; set; }
    public Phase CurrentPhase { get; set; }
    public int TurnNumber { get; set; }
    public bool IsGameOver { get; set; }
    public string? Winner { get; set; }
    public bool IsFirstTurn { get; set; }
    public CombatStep CombatStep { get; set; } = CombatStep.None;
    public CombatState? Combat { get; set; }
    private readonly List<IStackObject> _stack = new();
    private readonly object _stackLock = new();

    /// <summary>
    /// Thread-safe snapshot of the stack for UI rendering.
    /// Engine code should use StackPush/StackPopTop/StackPeekTop for mutations.
    /// </summary>
    public List<IStackObject> Stack
    {
        get { lock (_stackLock) { return _stack.ToList(); } }
    }

    public int StackCount { get { lock (_stackLock) { return _stack.Count; } } }

    public void StackPush(IStackObject item) { lock (_stackLock) { _stack.Add(item); } }

    public bool StackRemove(IStackObject item) { lock (_stackLock) { return _stack.Remove(item); } }

    public IStackObject? StackPeekTop()
    {
        lock (_stackLock) { return _stack.Count > 0 ? _stack[^1] : null; }
    }

    public IStackObject? StackPopTop()
    {
        lock (_stackLock)
        {
            if (_stack.Count == 0) return null;
            var top = _stack[^1];
            _stack.RemoveAt(_stack.Count - 1);
            return top;
        }
    }

    public Queue<Guid> ExtraTurns { get; } = new();
    public List<ContinuousEffect> ActiveEffects { get; } = new();
    public List<DelayedTrigger> DelayedTriggers { get; } = new();
    public long NextEffectTimestamp { get; set; } = 1;
    public event Action? OnStateChanged;

    private readonly object _logLock = new();
    private readonly List<string> _gameLog = new();

    /// <summary>
    /// Thread-safe snapshot of the game log. Returns a copy to avoid
    /// collection-modified-during-enumeration exceptions when the UI
    /// iterates while the game loop appends.
    /// </summary>
    public List<string> GameLog
    {
        get
        {
            lock (_logLock)
            {
                return _gameLog.ToList();
            }
        }
    }

    public GameState(Player player1, Player player2)
    {
        Player1 = player1;
        Player2 = player2;
        ActivePlayer = player1;
        PriorityPlayer = player1;
        CurrentPhase = Phase.Untap;
        TurnNumber = 1;
    }

    public Player GetOpponent(Player player) =>
        player == Player1 ? Player2 : Player1;

    public Player GetPlayer(Guid playerId) =>
        playerId == Player1.Id ? Player1 : Player2;

    public void Log(string message)
    {
        lock (_logLock)
        {
            _gameLog.Add(message);
        }
        OnStateChanged?.Invoke();
    }
}
