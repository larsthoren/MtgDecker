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

    [Theory]
    [InlineData("Goblin King", ContinuousEffectType.ModifyPowerToughness, EffectLayer.Layer7c_ModifyPT)]
    [InlineData("Goblin King", ContinuousEffectType.GrantKeyword, EffectLayer.Layer6_AbilityAddRemove)]
    [InlineData("Goblin Warchief", ContinuousEffectType.GrantKeyword, EffectLayer.Layer6_AbilityAddRemove)]
    [InlineData("Deranged Hermit", ContinuousEffectType.ModifyPowerToughness, EffectLayer.Layer7c_ModifyPT)]
    [InlineData("Opalescence", ContinuousEffectType.BecomeCreature, EffectLayer.Layer4_TypeChanging)]
    [InlineData("Goblin Guide", ContinuousEffectType.GrantKeyword, EffectLayer.Layer6_AbilityAddRemove)]
    [InlineData("Nimble Mongoose", ContinuousEffectType.GrantKeyword, EffectLayer.Layer6_AbilityAddRemove)]
    [InlineData("Nimble Mongoose", ContinuousEffectType.ModifyPowerToughness, EffectLayer.Layer7c_ModifyPT)]
    [InlineData("Argothian Enchantress", ContinuousEffectType.GrantKeyword, EffectLayer.Layer6_AbilityAddRemove)]
    [InlineData("Sterling Grove", ContinuousEffectType.GrantKeyword, EffectLayer.Layer6_AbilityAddRemove)]
    public void CardDefinition_ContinuousEffects_HaveCorrectLayer(string cardName, ContinuousEffectType effectType, EffectLayer expectedLayer)
    {
        CardDefinitions.TryGet(cardName, out var def).Should().BeTrue($"{cardName} should exist");
        var matching = def!.ContinuousEffects.Where(e => e.Type == effectType).ToList();
        matching.Should().NotBeEmpty($"{cardName} should have {effectType} effect");
        matching.First().Layer.Should().Be(expectedLayer, $"{cardName}'s {effectType} should be in {expectedLayer}");
    }

    [Fact]
    public void CardDefinition_NonLayeredEffects_HaveNullLayer()
    {
        // Exploration's ExtraLandDrop effect should not have a layer
        CardDefinitions.TryGet("Exploration", out var def).Should().BeTrue();
        def!.ContinuousEffects.Should().ContainSingle();
        def.ContinuousEffects[0].Layer.Should().BeNull();

        // Solitary Confinement's effects should not have layers
        CardDefinitions.TryGet("Solitary Confinement", out var solDef).Should().BeTrue();
        solDef!.ContinuousEffects.Should().AllSatisfy(e => e.Layer.Should().BeNull());
    }

    [Fact]
    public void CardDefinition_GraveyardAbilities_HaveCorrectLayer()
    {
        CardDefinitions.TryGet("Anger", out var def).Should().BeTrue();
        def!.GraveyardAbilities.Should().ContainSingle();
        def.GraveyardAbilities[0].Layer.Should().Be(EffectLayer.Layer6_AbilityAddRemove);
    }
}
