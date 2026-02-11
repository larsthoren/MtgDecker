using FluentAssertions;
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Tests;

public class StackObjectTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        var card = new GameCard { Name = "Lightning Bolt" };
        var controllerId = Guid.NewGuid();
        var target = new TargetInfo(Guid.NewGuid(), controllerId, Enums.ZoneType.Battlefield);

        var obj = new StackObject(card, controllerId, new Dictionary<ManaColor, int>(), new List<TargetInfo> { target }, 1);

        obj.Id.Should().NotBeEmpty();
        obj.Card.Should().Be(card);
        obj.ControllerId.Should().Be(controllerId);
        obj.Targets.Should().ContainSingle().Which.Should().Be(target);
        obj.Timestamp.Should().Be(1);
    }

    [Fact]
    public void Constructor_WithNoTargets_HasEmptyList()
    {
        var card = new GameCard { Name = "Forest" };
        var controllerId = Guid.NewGuid();

        var obj = new StackObject(card, controllerId, new Dictionary<ManaColor, int>(), new List<TargetInfo>(), 0);

        obj.Targets.Should().BeEmpty();
    }
}
