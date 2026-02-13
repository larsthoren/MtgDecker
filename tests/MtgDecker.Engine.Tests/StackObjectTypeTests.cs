// tests/MtgDecker.Engine.Tests/StackObjectTypeTests.cs
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Triggers;
using NSubstitute;

namespace MtgDecker.Engine.Tests;

public class StackObjectTypeTests
{
    [Fact]
    public void StackObject_Implements_IStackObject()
    {
        var card = new GameCard { Name = "Lightning Bolt" };
        var so = new StackObject(card, Guid.NewGuid(), new(), new(), 1);
        IStackObject iface = so;
        iface.ControllerId.Should().Be(so.ControllerId);
    }

    [Fact]
    public void TriggeredAbilityStackObject_Implements_IStackObject()
    {
        var source = new GameCard { Name = "Goblin Matron" };
        var effect = Substitute.For<IEffect>();
        var controllerId = Guid.NewGuid();

        var taso = new TriggeredAbilityStackObject(source, controllerId, effect);

        IStackObject iface = taso;
        iface.ControllerId.Should().Be(controllerId);
        taso.Source.Should().Be(source);
        taso.Effect.Should().Be(effect);
        taso.Target.Should().BeNull();
        taso.TargetPlayerId.Should().BeNull();
    }

    [Fact]
    public void TriggeredAbilityStackObject_With_Target()
    {
        var source = new GameCard { Name = "Sharpshooter" };
        var target = new GameCard { Name = "Elf" };
        var effect = Substitute.For<IEffect>();
        var controllerId = Guid.NewGuid();

        var taso = new TriggeredAbilityStackObject(source, controllerId, effect, target);

        taso.Target.Should().Be(target);
    }

    [Fact]
    public void TriggeredAbilityStackObject_Has_Unique_Id()
    {
        var effect = Substitute.For<IEffect>();
        var t1 = new TriggeredAbilityStackObject(new GameCard(), Guid.NewGuid(), effect);
        var t2 = new TriggeredAbilityStackObject(new GameCard(), Guid.NewGuid(), effect);

        t1.Id.Should().NotBe(t2.Id);
    }
}
