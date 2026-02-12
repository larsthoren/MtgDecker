using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Tests;

public class ContinuousEffectTypeTests
{
    [Fact]
    public void ContinuousEffect_Can_Be_Created_With_PowerToughness_Modifier()
    {
        var effect = new ContinuousEffect(
            Guid.NewGuid(),
            ContinuousEffectType.ModifyPowerToughness,
            (card, player) => card.IsCreature,
            PowerMod: 1, ToughnessMod: 1);

        effect.Type.Should().Be(ContinuousEffectType.ModifyPowerToughness);
        effect.PowerMod.Should().Be(1);
        effect.ToughnessMod.Should().Be(1);
    }

    [Fact]
    public void ContinuousEffect_Can_Be_Created_With_Keyword_Grant()
    {
        var effect = new ContinuousEffect(
            Guid.NewGuid(),
            ContinuousEffectType.GrantKeyword,
            (card, player) => card.IsCreature,
            GrantedKeyword: Keyword.Haste);

        effect.GrantedKeyword.Should().Be(Keyword.Haste);
    }

    [Fact]
    public void ContinuousEffect_Can_Be_Created_With_Cost_Modification()
    {
        var effect = new ContinuousEffect(
            Guid.NewGuid(),
            ContinuousEffectType.ModifyCost,
            (card, player) => true,
            CostMod: -1,
            CostApplies: c => c.Subtypes.Contains("Goblin"));

        effect.CostMod.Should().Be(-1);
        effect.CostApplies.Should().NotBeNull();
    }

    [Fact]
    public void ContinuousEffect_Can_Be_Created_With_ExtraLandDrop()
    {
        var effect = new ContinuousEffect(
            Guid.NewGuid(),
            ContinuousEffectType.ExtraLandDrop,
            (card, player) => true,
            ExtraLandDrops: 1);

        effect.ExtraLandDrops.Should().Be(1);
    }

    [Fact]
    public void FetchAbility_Stores_SearchTypes()
    {
        var fetch = new FetchAbility(["Mountain", "Forest"]);
        fetch.SearchTypes.Should().BeEquivalentTo(["Mountain", "Forest"]);
    }
}
