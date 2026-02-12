namespace MtgDecker.Engine.Simulation;

public record SimulationResult(
    string? WinnerName,
    string? LoserName,
    bool IsDraw,
    int TotalTurns,
    int Player1FinalLife,
    int Player2FinalLife,
    IReadOnlyList<string> GameLog,
    TimeSpan Duration);
