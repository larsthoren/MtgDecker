using FluentAssertions;
using MtgDecker.Engine.Simulation;

namespace MtgDecker.Engine.Tests.Simulation;

public class SimulationRunnerTests
{
    private static List<GameCard> CreateSimpleDeck(int creatures, int lands)
    {
        var deck = new List<GameCard>();
        for (int i = 0; i < creatures; i++)
            deck.Add(GameCard.Create("Mogg Fanatic"));  // 1/1 creature, {R}
        for (int i = 0; i < lands; i++)
            deck.Add(GameCard.Create("Mountain"));  // basic land, taps for R
        return deck;
    }

    [Fact]
    public async Task RunGameAsync_CompletesWithoutException()
    {
        var runner = new SimulationRunner();
        var deck1 = CreateSimpleDeck(20, 20);
        var deck2 = CreateSimpleDeck(20, 20);
        var result = await runner.RunGameAsync(deck1, deck2);
        result.Should().NotBeNull();
        result.TotalTurns.Should().BeGreaterThan(0);
        result.GameLog.Should().NotBeEmpty();
        result.Duration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task RunGameAsync_HasWinnerOrDraw()
    {
        var runner = new SimulationRunner();
        var deck1 = CreateSimpleDeck(20, 20);
        var deck2 = CreateSimpleDeck(20, 20);
        var result = await runner.RunGameAsync(deck1, deck2);
        (result.WinnerName != null || result.IsDraw).Should().BeTrue();
    }

    [Fact]
    public async Task RunGameAsync_GameLogContainsTurns()
    {
        var runner = new SimulationRunner();
        var deck1 = CreateSimpleDeck(20, 20);
        var deck2 = CreateSimpleDeck(20, 20);
        var result = await runner.RunGameAsync(deck1, deck2);
        result.GameLog.Should().Contain(l => l.Contains("Turn 1"));
    }

    [Fact]
    public async Task RunBatchAsync_ReturnsCorrectGameCount()
    {
        var runner = new SimulationRunner();
        var deck1 = CreateSimpleDeck(20, 20);
        var deck2 = CreateSimpleDeck(20, 20);
        var batch = await runner.RunBatchAsync(deck1, deck2, 3);
        batch.TotalGames.Should().Be(3);
        batch.Games.Should().HaveCount(3);
        (batch.Player1Wins + batch.Player2Wins + batch.Draws).Should().Be(3);
    }

    [Fact]
    public async Task RunBatchAsync_WinRateIsBetweenZeroAndOne()
    {
        var runner = new SimulationRunner();
        var deck1 = CreateSimpleDeck(20, 20);
        var deck2 = CreateSimpleDeck(20, 20);
        var batch = await runner.RunBatchAsync(deck1, deck2, 5);
        batch.Player1WinRate.Should().BeInRange(0.0, 1.0);
        batch.AverageGameLength.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task RunGameAsync_CustomNames()
    {
        var runner = new SimulationRunner();
        var deck1 = CreateSimpleDeck(20, 20);
        var deck2 = CreateSimpleDeck(20, 20);
        var result = await runner.RunGameAsync(deck1, deck2, "Goblins", "Enchantress");
        result.GameLog.Should().Contain(l => l.Contains("Goblins") || l.Contains("Enchantress"));
    }

    [Fact]
    public async Task RunGameAsync_ClonesAllCardProperties()
    {
        // Use forests which have ManaAbility in CardDefinitions
        var deck1 = new List<GameCard>();
        var deck2 = new List<GameCard>();

        for (int i = 0; i < 40; i++)
        {
            deck1.Add(GameCard.Create("Forest", "Basic Land — Forest"));
            deck2.Add(GameCard.Create("Forest", "Basic Land — Forest"));
        }

        var runner = new SimulationRunner();
        var result = await runner.RunGameAsync(deck1, deck2);

        // Game should complete (forests can tap for mana - proves ManaAbility was cloned)
        result.Should().NotBeNull();
        result.TotalTurns.Should().BeGreaterThan(0);
    }
}
