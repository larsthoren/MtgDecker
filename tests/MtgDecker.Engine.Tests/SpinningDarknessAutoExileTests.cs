using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class SpinningDarknessAutoExileTests
{
    private (GameEngine engine, GameState state, TestDecisionHandler h1, TestDecisionHandler h2) CreateSetup()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);

        for (int i = 0; i < 40; i++)
        {
            p1.Library.Add(new GameCard { Name = $"Card{i}" });
            p2.Library.Add(new GameCard { Name = $"Card{i}" });
        }

        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);
        return (engine, state, h1, h2);
    }

    [Fact]
    public async Task SpinningDarkness_ExilesTop3BlackCards_NotPlayerChoice()
    {
        var (engine, state, h1, _) = CreateSetup();

        // Put 5 cards in graveyard:
        // Bottom: NonBlack1, Black1, Black2, NonBlack2, Black3 (top)
        // "Top" = last added = end of list
        var nonBlack1 = new GameCard { Name = "NonBlack1", ManaCost = ManaCost.Parse("{1}{G}") };
        var black1 = new GameCard { Name = "Black1", ManaCost = ManaCost.Parse("{B}") };
        var black2 = new GameCard { Name = "Black2", ManaCost = ManaCost.Parse("{1}{B}") };
        var nonBlack2 = new GameCard { Name = "NonBlack2", ManaCost = ManaCost.Parse("{2}{R}") };
        var black3 = new GameCard { Name = "Black3", ManaCost = ManaCost.Parse("{2}{B}{B}") };

        state.Player1.Graveyard.Add(nonBlack1);
        state.Player1.Graveyard.Add(black1);
        state.Player1.Graveyard.Add(black2);
        state.Player1.Graveyard.Add(nonBlack2);
        state.Player1.Graveyard.Add(black3);

        var spinningDarkness = GameCard.Create("Spinning Darkness", "Instant");
        state.Player1.Hand.Add(spinningDarkness);

        CardDefinitions.TryGet("Spinning Darkness", out var def).Should().BeTrue();
        await engine.PayAlternateCostAsync(def!.AlternateCost!, state.Player1, spinningDarkness, CancellationToken.None);

        // Top 3 black cards from graveyard should be exiled: Black3, Black2, Black1
        // (from top = end of list, working downward)
        state.Player1.Exile.Cards.Select(c => c.Name).Should().BeEquivalentTo(
            new[] { "Black3", "Black2", "Black1" });

        // NonBlack cards should remain in graveyard
        state.Player1.Graveyard.Cards.Select(c => c.Name).Should().BeEquivalentTo(
            new[] { "NonBlack1", "NonBlack2" });
    }
}
