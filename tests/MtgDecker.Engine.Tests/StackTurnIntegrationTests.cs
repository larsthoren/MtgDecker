using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class StackTurnIntegrationTests
{
    [Fact]
    public async Task FullTurn_CastAndResolve_ViaRunTurnAsync()
    {
        // Arrange
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
        await engine.StartGameAsync();

        // Put a Mountain and Mogg Fanatic in P1's hand
        var mountain = GameCard.Create("Mountain", "Basic Land — Mountain");
        p1.Hand.Add(mountain);
        var goblin = GameCard.Create("Mogg Fanatic", "Creature — Goblin");
        p1.Hand.Add(goblin);

        // Enqueue actions in order consumed by the priority system:
        // Upkeep: P1 passes (P2 auto-passes when queue empty)
        h1.EnqueueAction(GameAction.Pass(p1.Id));
        // Draw: P1 passes (P2 auto-passes)
        h1.EnqueueAction(GameAction.Pass(p1.Id));
        // MainPhase1: play land, tap for mana, cast creature via stack
        h1.EnqueueAction(GameAction.PlayLand(p1.Id, mountain.Id));
        h1.EnqueueAction(GameAction.TapCard(p1.Id, mountain.Id));
        h1.EnqueueAction(GameAction.CastSpell(p1.Id, goblin.Id));
        // After cast, P1 passes priority; P2 auto-passes -> stack resolves
        // After resolve, both auto-pass -> advance out of MainPhase1
        h1.EnqueueAction(GameAction.Pass(p1.Id));
        // Combat: no eligible attackers (summoning sickness) -> skipped
        // MainPhase2, End: both auto-pass

        // Act
        await engine.RunTurnAsync();

        // Assert
        p1.Battlefield.Cards.Should().Contain(c => c.Name == "Mountain");
        p1.Battlefield.Cards.Should().Contain(c => c.Name == "Mogg Fanatic");
        state.Stack.Should().BeEmpty();
    }
}
