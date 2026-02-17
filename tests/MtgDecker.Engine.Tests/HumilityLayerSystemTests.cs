using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class HumilityLayerSystemTests
{
    [Fact]
    public void EffectLayer_HasExpectedValues()
    {
        ((int)EffectLayer.Layer4_TypeChanging).Should().Be(4);
        ((int)EffectLayer.Layer6_AbilityAddRemove).Should().Be(6);
        ((int)EffectLayer.Layer7a_CDA).Should().Be(70);
        ((int)EffectLayer.Layer7b_SetPT).Should().Be(71);
        ((int)EffectLayer.Layer7c_ModifyPT).Should().Be(72);
    }

    [Fact]
    public void ContinuousEffectType_HasNewValues()
    {
        var setPT = ContinuousEffectType.SetBasePowerToughness;
        var removeAbilities = ContinuousEffectType.RemoveAbilities;
        setPT.Should().NotBe(ContinuousEffectType.ModifyPowerToughness);
        removeAbilities.Should().NotBe(ContinuousEffectType.ModifyPowerToughness);
    }

    [Fact]
    public void ContinuousEffect_SupportsLayerAndTimestampFields()
    {
        var effect = new ContinuousEffect(
            Guid.NewGuid(),
            ContinuousEffectType.SetBasePowerToughness,
            (_, _) => true,
            Layer: EffectLayer.Layer7b_SetPT,
            Timestamp: 42,
            SetPower: 1,
            SetToughness: 1);

        effect.Layer.Should().Be(EffectLayer.Layer7b_SetPT);
        effect.Timestamp.Should().Be(42);
        effect.SetPower.Should().Be(1);
        effect.SetToughness.Should().Be(1);
    }

    [Fact]
    public void GameCard_HasAbilitiesRemovedField()
    {
        var card = new GameCard();
        card.AbilitiesRemoved.Should().BeFalse();
        card.AbilitiesRemoved = true;
        card.AbilitiesRemoved.Should().BeTrue();
    }

    [Fact]
    public void GameState_HasNextEffectTimestamp()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        state.NextEffectTimestamp.Should().Be(1);
        state.NextEffectTimestamp++;
        state.NextEffectTimestamp.Should().Be(2);
    }
}
