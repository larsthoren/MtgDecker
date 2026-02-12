namespace MtgDecker.Engine.Simulation;

public record BatchResult(
    int TotalGames,
    int Player1Wins,
    int Player2Wins,
    int Draws,
    double Player1WinRate,
    double AverageGameLength,
    double AverageLifeDifferential,
    IReadOnlyList<SimulationResult> Games);
