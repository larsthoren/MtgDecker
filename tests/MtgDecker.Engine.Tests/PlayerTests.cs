using FluentAssertions;
using NSubstitute;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Tests;

public class PlayerTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        var id = Guid.NewGuid();
        var handler = Substitute.For<IPlayerDecisionHandler>();

        var player = new Player(id, "Alice", handler);

        player.Id.Should().Be(id);
        player.Name.Should().Be("Alice");
        player.DecisionHandler.Should().BeSameAs(handler);
        player.Life.Should().Be(20);
    }

    [Fact]
    public void Constructor_InitializesEmptyZones()
    {
        var player = new Player(Guid.NewGuid(), "Alice", Substitute.For<IPlayerDecisionHandler>());

        player.Library.Type.Should().Be(ZoneType.Library);
        player.Library.Count.Should().Be(0);
        player.Hand.Type.Should().Be(ZoneType.Hand);
        player.Hand.Count.Should().Be(0);
        player.Battlefield.Type.Should().Be(ZoneType.Battlefield);
        player.Battlefield.Count.Should().Be(0);
        player.Graveyard.Type.Should().Be(ZoneType.Graveyard);
        player.Graveyard.Count.Should().Be(0);
        player.Exile.Type.Should().Be(ZoneType.Exile);
        player.Exile.Count.Should().Be(0);
    }

    [Fact]
    public void AdjustLife_ChangesLifeTotal()
    {
        var player = new Player(Guid.NewGuid(), "Alice", Substitute.For<IPlayerDecisionHandler>());

        player.AdjustLife(-5);
        player.Life.Should().Be(15);

        player.AdjustLife(3);
        player.Life.Should().Be(18);
    }

    [Theory]
    [InlineData(ZoneType.Library)]
    [InlineData(ZoneType.Hand)]
    [InlineData(ZoneType.Battlefield)]
    [InlineData(ZoneType.Graveyard)]
    [InlineData(ZoneType.Exile)]
    public void GetZone_ReturnsCorrectZone(ZoneType type)
    {
        var player = new Player(Guid.NewGuid(), "Alice", Substitute.For<IPlayerDecisionHandler>());

        var zone = player.GetZone(type);

        zone.Type.Should().Be(type);
    }
}
