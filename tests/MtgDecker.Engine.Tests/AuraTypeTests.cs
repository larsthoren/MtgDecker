using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Triggers;

namespace MtgDecker.Engine.Tests;

public class AuraTypeTests
{
    [Fact]
    public void GameCard_AttachedTo_Defaults_Null()
    {
        var card = new GameCard();
        card.AttachedTo.Should().BeNull();
    }

    [Fact]
    public void GameCard_AttachedTo_Can_Be_Set()
    {
        var targetId = Guid.NewGuid();
        var card = new GameCard { AttachedTo = targetId };
        card.AttachedTo.Should().Be(targetId);
    }

    [Fact]
    public void WildGrowth_Has_AuraTarget_Land()
    {
        CardDefinitions.TryGet("Wild Growth", out var def).Should().BeTrue();
        def!.AuraTarget.Should().Be(AuraTarget.Land);
    }

    [Fact]
    public void GameEvent_TapForMana_Exists()
    {
        Enum.IsDefined(GameEvent.TapForMana).Should().BeTrue();
    }

    [Fact]
    public void TriggerCondition_AttachedPermanentTapped_Exists()
    {
        Enum.IsDefined(TriggerCondition.AttachedPermanentTapped).Should().BeTrue();
    }
}
