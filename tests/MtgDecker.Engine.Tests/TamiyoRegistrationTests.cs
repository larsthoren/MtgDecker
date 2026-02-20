using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class TamiyoRegistrationTests
{
    [Fact]
    public void Tamiyo_FrontFace_IsRegistered()
    {
        CardDefinitions.TryGet("Tamiyo, Inquisitive Student", out var def).Should().BeTrue();
        def!.CardTypes.Should().HaveFlag(CardType.Creature);
        def.Name.Should().Be("Tamiyo, Inquisitive Student");
    }

    [Fact]
    public void Tamiyo_FrontFace_HasCorrectStats()
    {
        CardDefinitions.TryGet("Tamiyo, Inquisitive Student", out var def);
        def!.Power.Should().Be(0);
        def.Toughness.Should().Be(3);
        def.IsLegendary.Should().BeTrue();
        def.Subtypes.Should().Contain("Moonfolk");
        def.Subtypes.Should().Contain("Wizard");
    }

    [Fact]
    public void Tamiyo_FrontFace_HasFlying()
    {
        CardDefinitions.TryGet("Tamiyo, Inquisitive Student", out var def);
        def!.ContinuousEffects.Should().Contain(e =>
            e.Type == ContinuousEffectType.GrantKeyword &&
            e.GrantedKeyword == Keyword.Flying);
    }

    [Fact]
    public void Tamiyo_FrontFace_HasAttackTrigger()
    {
        CardDefinitions.TryGet("Tamiyo, Inquisitive Student", out var def);
        def!.Triggers.Should().Contain(t =>
            t.Event == GameEvent.BeginCombat &&
            t.Condition == TriggerCondition.SelfAttacks &&
            t.Effect is InvestigateEffect);
    }

    [Fact]
    public void Tamiyo_FrontFace_HasThirdDrawTransformTrigger()
    {
        CardDefinitions.TryGet("Tamiyo, Inquisitive Student", out var def);
        def!.Triggers.Should().Contain(t =>
            t.Event == GameEvent.DrawCard &&
            t.Condition == TriggerCondition.ThirdDrawInTurn &&
            t.Effect is TransformExileReturnEffect);
    }

    [Fact]
    public void Tamiyo_FrontFace_HasTransformInto()
    {
        CardDefinitions.TryGet("Tamiyo, Inquisitive Student", out var def);
        def!.TransformInto.Should().NotBeNull();
        def.TransformInto!.Name.Should().Be("Tamiyo, Seasoned Scholar");
    }

    [Fact]
    public void Tamiyo_BackFace_IsPlaneswalker()
    {
        CardDefinitions.TryGet("Tamiyo, Inquisitive Student", out var def);
        var back = def!.TransformInto!;
        back.CardTypes.Should().HaveFlag(CardType.Planeswalker);
        back.IsLegendary.Should().BeTrue();
        back.Subtypes.Should().Contain("Tamiyo");
    }

    [Fact]
    public void Tamiyo_BackFace_HasStartingLoyalty2()
    {
        CardDefinitions.TryGet("Tamiyo, Inquisitive Student", out var def);
        var back = def!.TransformInto!;
        back.StartingLoyalty.Should().Be(2);
    }

    [Fact]
    public void Tamiyo_BackFace_HasThreeLoyaltyAbilities()
    {
        CardDefinitions.TryGet("Tamiyo, Inquisitive Student", out var def);
        var back = def!.TransformInto!;
        back.LoyaltyAbilities.Should().HaveCount(3);
    }

    [Fact]
    public void Tamiyo_BackFace_PlusTwoIsDefenseEffect()
    {
        CardDefinitions.TryGet("Tamiyo, Inquisitive Student", out var def);
        var back = def!.TransformInto!;
        var ability = back.LoyaltyAbilities![0];
        ability.LoyaltyCost.Should().Be(2);
        ability.Effect.Should().BeOfType<TamiyoDefenseEffect>();
    }

    [Fact]
    public void Tamiyo_BackFace_MinusThreeIsRecoverEffect()
    {
        CardDefinitions.TryGet("Tamiyo, Inquisitive Student", out var def);
        var back = def!.TransformInto!;
        var ability = back.LoyaltyAbilities![1];
        ability.LoyaltyCost.Should().Be(-3);
        ability.Effect.Should().BeOfType<TamiyoRecoverEffect>();
    }

    [Fact]
    public void Tamiyo_BackFace_MinusSevenIsUltimateEffect()
    {
        CardDefinitions.TryGet("Tamiyo, Inquisitive Student", out var def);
        var back = def!.TransformInto!;
        var ability = back.LoyaltyAbilities![2];
        ability.LoyaltyCost.Should().Be(-7);
        ability.Effect.Should().BeOfType<TamiyoUltimateEffect>();
    }
}
