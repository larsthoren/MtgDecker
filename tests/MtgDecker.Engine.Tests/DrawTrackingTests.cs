using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class DrawTrackingTests
{
    [Fact]
    public void Player_DrawsThisTurn_StartsAtZero()
    {
        var handler = new TestDecisionHandler();
        var player = new Player(Guid.NewGuid(), "Test", handler);
        player.DrawsThisTurn.Should().Be(0);
    }

    [Fact]
    public void Player_DrawStepDrawExempted_StartsAsFalse()
    {
        var handler = new TestDecisionHandler();
        var player = new Player(Guid.NewGuid(), "Test", handler);
        player.DrawStepDrawExempted.Should().BeFalse();
    }

    private static (GameEngine engine, GameState state, Player p1, Player p2,
        TestDecisionHandler h1, TestDecisionHandler h2) SetupGame()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);
        return (engine, state, p1, p2, h1, h2);
    }

    [Fact]
    public void DrawCards_IncrementsDrawsThisTurn()
    {
        var (engine, state, p1, _, _, _) = SetupGame();
        p1.Library.Add(GameCard.Create("Card1", "Instant"));
        p1.Library.Add(GameCard.Create("Card2", "Instant"));
        p1.Library.Add(GameCard.Create("Card3", "Instant"));

        engine.DrawCards(p1, 3);

        p1.DrawsThisTurn.Should().Be(3);
        p1.Hand.Count.Should().Be(3);
    }

    [Fact]
    public void DrawCards_DrawStepDraw_ExemptsFirstDraw()
    {
        var (engine, state, p1, _, _, _) = SetupGame();
        p1.Library.Add(GameCard.Create("Card1", "Instant"));

        engine.DrawCards(p1, 1, isDrawStepDraw: true);

        p1.DrawsThisTurn.Should().Be(1);
        p1.DrawStepDrawExempted.Should().BeTrue();
    }

    [Fact]
    public void DrawCards_DrawStepDraw_SecondDrawNotExempted()
    {
        var (engine, state, p1, _, _, _) = SetupGame();
        p1.Library.Add(GameCard.Create("Card1", "Instant"));
        p1.Library.Add(GameCard.Create("Card2", "Instant"));

        // Two cards drawn during draw step (e.g., Howling Mine)
        engine.DrawCards(p1, 2, isDrawStepDraw: true);

        p1.DrawsThisTurn.Should().Be(2);
        p1.DrawStepDrawExempted.Should().BeTrue(); // First was exempted
        // But second draw was NOT exempted (it was tracked normally)
    }
}
