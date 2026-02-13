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
    public List<IStackObject> Stack { get; } = new();
    public List<ContinuousEffect> ActiveEffects { get; } = new();
    public List<DelayedTrigger> DelayedTriggers { get; } = new();
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
