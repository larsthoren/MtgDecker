using FluentAssertions;
using MtgDecker.Engine.Simulation;

namespace MtgDecker.Engine.Tests.Simulation;

public class SimulationIntegrationTests
{
    private static List<GameCard> CreateGoblinDeck()
    {
        var deck = new List<GameCard>();

        // 24 Mountains
        for (int i = 0; i < 24; i++)
            deck.Add(GameCard.Create("Mountain", "Basic Land — Mountain"));

        // 4x each core creature
        for (int i = 0; i < 4; i++)
        {
            deck.Add(GameCard.Create("Goblin Lackey"));
            deck.Add(GameCard.Create("Mogg Fanatic"));
            deck.Add(GameCard.Create("Goblin Piledriver"));
            deck.Add(GameCard.Create("Goblin Warchief"));
        }

        // 4x Goblin Matron (ETB: search for Goblin)
        for (int i = 0; i < 4; i++)
            deck.Add(GameCard.Create("Goblin Matron"));

        // 4x Goblin Ringleader (ETB: reveal top 4)
        for (int i = 0; i < 4; i++)
            deck.Add(GameCard.Create("Goblin Ringleader"));

        // 2x Siege-Gang Commander (ETB: create 3 tokens)
        for (int i = 0; i < 2; i++)
            deck.Add(GameCard.Create("Siege-Gang Commander"));

        // Pad to 60
        while (deck.Count < 60)
            deck.Add(GameCard.Create("Skirk Prospector"));

        return deck;
    }

    [Fact]
    public async Task FullGame_GoblinsVsGoblins_CompletesWithWinner()
    {
        var runner = new SimulationRunner();
        var result = await runner.RunGameAsync(CreateGoblinDeck(), CreateGoblinDeck(), "Goblins A", "Goblins B");
        result.TotalTurns.Should().BeGreaterThan(1);
        result.GameLog.Should().NotBeEmpty();
        (result.WinnerName != null || result.IsDraw).Should().BeTrue();
    }

    [Fact]
    public async Task FullGame_ProducesCombat()
    {
        var runner = new SimulationRunner();
        var result = await runner.RunGameAsync(CreateGoblinDeck(), CreateGoblinDeck(), "Goblins A", "Goblins B");
        result.GameLog.Should().Contain(l => l.Contains("attacks") || l.Contains("damage") || l.Contains("deals"));
    }

    [Fact]
    public async Task FullGame_TriggersFireDuringPlay()
    {
        var runner = new SimulationRunner();
        var result = await runner.RunGameAsync(CreateGoblinDeck(), CreateGoblinDeck(), "Goblins A", "Goblins B");
        result.GameLog.Should().Contain(l =>
            l.Contains("triggers") ||
            l.Contains("searches library") ||
            l.Contains("creates a") ||
            l.Contains("Revealed"));
    }

    [Fact]
    public async Task BatchRun_FiveGames_AllComplete()
    {
        var runner = new SimulationRunner();
        var batch = await runner.RunBatchAsync(CreateGoblinDeck(), CreateGoblinDeck(), 5, "Goblins A", "Goblins B");
        batch.TotalGames.Should().Be(5);
        batch.Games.Should().OnlyContain(g => g.TotalTurns > 0);
        batch.AverageGameLength.Should().BeGreaterThan(0);
    }
}
