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
    public List<StackObject> Stack { get; } = new();
    public List<string> GameLog { get; } = new();
    public event Action? OnStateChanged;

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

    public void Log(string message)
    {
        GameLog.Add(message);
        OnStateChanged?.Invoke();
    }
}
