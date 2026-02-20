using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class LifeLostTrackingTests
{
    [Fact]
    public void Player_LifeLostThisTurn_StartsAtZero()
    {
        var handler = new TestDecisionHandler();
        var player = new Player(Guid.NewGuid(), "Test", handler);
        player.LifeLostThisTurn.Should().Be(0);
    }

    [Fact]
    public void Player_AdjustLife_NegativeDelta_TracksLifeLost()
    {
        var handler = new TestDecisionHandler();
        var player = new Player(Guid.NewGuid(), "Test", handler);

        player.AdjustLife(-3);

        player.LifeLostThisTurn.Should().Be(3);
        player.Life.Should().Be(17);
    }

    [Fact]
    public void Player_AdjustLife_PositiveDelta_DoesNotTrackLifeLost()
    {
        var handler = new TestDecisionHandler();
        var player = new Player(Guid.NewGuid(), "Test", handler);

        player.AdjustLife(-5);
        player.AdjustLife(3);

        player.LifeLostThisTurn.Should().Be(5);
        player.Life.Should().Be(18);
    }

    [Fact]
    public void Player_LifeLostThisTurn_AccumulatesAcrossMultipleLosses()
    {
        var handler = new TestDecisionHandler();
        var player = new Player(Guid.NewGuid(), "Test", handler);

        player.AdjustLife(-2);
        player.AdjustLife(-3);

        player.LifeLostThisTurn.Should().Be(5);
    }
}
