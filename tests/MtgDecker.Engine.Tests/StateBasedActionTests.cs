using FluentAssertions;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class StateBasedActionTests
{
    [Fact]
    public void CheckStateBasedActions_LifeAtZero_SetsGameOver()
    {
        var p1 = new Player(Guid.NewGuid(), "Player 1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "Player 2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        p1.AdjustLife(-20); // life = 0

        engine.CheckStateBasedActions();

        state.IsGameOver.Should().BeTrue();
        state.Winner.Should().Be("Player 2");
    }

    [Fact]
    public void CheckStateBasedActions_LifeBelowZero_SetsGameOver()
    {
        var p1 = new Player(Guid.NewGuid(), "Player 1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "Player 2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        p2.AdjustLife(-25); // life = -5

        engine.CheckStateBasedActions();

        state.IsGameOver.Should().BeTrue();
        state.Winner.Should().Be("Player 1");
    }

    [Fact]
    public void CheckStateBasedActions_BothAlive_DoesNotEndGame()
    {
        var p1 = new Player(Guid.NewGuid(), "Player 1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "Player 2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        engine.CheckStateBasedActions();

        state.IsGameOver.Should().BeFalse();
        state.Winner.Should().BeNull();
    }

    [Fact]
    public void CheckStateBasedActions_BothAtZero_SetsGameOver()
    {
        var p1 = new Player(Guid.NewGuid(), "Player 1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "Player 2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        p1.AdjustLife(-20);
        p2.AdjustLife(-20);

        engine.CheckStateBasedActions();

        // Both at 0 — draw (both lose simultaneously)
        state.IsGameOver.Should().BeTrue();
    }
}
