using System.Diagnostics;
using MtgDecker.Engine.AI;

namespace MtgDecker.Engine.Simulation;

public class SimulationRunner
{
    private const int MaxTurns = 100;

    public async Task<SimulationResult> RunGameAsync(
        IReadOnlyList<GameCard> deck1,
        IReadOnlyList<GameCard> deck2,
        string player1Name = "Bot A",
        string player2Name = "Bot B",
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        var p1 = new Player(Guid.NewGuid(), player1Name, new AiBotDecisionHandler { ActionDelayMs = 0 });
        var p2 = new Player(Guid.NewGuid(), player2Name, new AiBotDecisionHandler { ActionDelayMs = 0 });

        foreach (var card in deck1) p1.Library.Add(CloneCard(card));
        foreach (var card in deck2) p2.Library.Add(CloneCard(card));

        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        await engine.StartGameAsync(ct);
        state.IsFirstTurn = true;

        while (!state.IsGameOver && state.TurnNumber <= MaxTurns)
        {
            ct.ThrowIfCancellationRequested();
            await engine.RunTurnAsync(ct);
        }

        if (!state.IsGameOver)
        {
            state.IsGameOver = true;
            state.Log($"Game ended in a draw after {MaxTurns} turns.");
        }

        sw.Stop();

        var winnerName = state.Winner;
        var loserName = winnerName == null ? null
            : winnerName == player1Name ? player2Name : player1Name;

        return new SimulationResult(
            WinnerName: winnerName,
            LoserName: loserName,
            IsDraw: winnerName == null,
            TotalTurns: state.TurnNumber,
            Player1FinalLife: p1.Life,
            Player2FinalLife: p2.Life,
            GameLog: state.GameLog.ToList(),
            Duration: sw.Elapsed);
    }

    public async Task<BatchResult> RunBatchAsync(
        IReadOnlyList<GameCard> deck1,
        IReadOnlyList<GameCard> deck2,
        int gameCount,
        string player1Name = "Bot A",
        string player2Name = "Bot B",
        CancellationToken ct = default)
    {
        var results = new List<SimulationResult>();
        for (int i = 0; i < gameCount; i++)
        {
            ct.ThrowIfCancellationRequested();
            results.Add(await RunGameAsync(deck1, deck2, player1Name, player2Name, ct));
        }

        var p1Wins = results.Count(r => r.WinnerName == player1Name);
        var p2Wins = results.Count(r => r.WinnerName == player2Name);

        return new BatchResult(
            TotalGames: gameCount,
            Player1Wins: p1Wins,
            Player2Wins: p2Wins,
            Draws: results.Count(r => r.IsDraw),
            Player1WinRate: gameCount > 0 ? (double)p1Wins / gameCount : 0,
            AverageGameLength: results.Average(r => r.TotalTurns),
            AverageLifeDifferential: results.Average(r => Math.Abs(r.Player1FinalLife - r.Player2FinalLife)),
            Games: results);
    }

    private static GameCard CloneCard(GameCard original) => new()
    {
        Name = original.Name,
        TypeLine = original.TypeLine,
        ImageUrl = original.ImageUrl,
        ManaCost = original.ManaCost,
        ManaAbility = original.ManaAbility,
        Power = original.Power,
        Toughness = original.Toughness,
        CardTypes = original.CardTypes,
        Subtypes = original.Subtypes,
        Triggers = original.Triggers,
        IsToken = original.IsToken,
        IsLegendary = original.IsLegendary,
        FetchAbility = original.FetchAbility,
    };
}
