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
}
