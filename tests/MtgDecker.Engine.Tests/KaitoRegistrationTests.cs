using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class KaitoRegistrationTests
{
    [Fact]
    public void CardDefinition_Kaito_IsRegistered()
    {
        CardDefinitions.TryGet("Kaito, Bane of Nightmares", out var def).Should().BeTrue();
        def!.ManaCost!.ConvertedManaCost.Should().Be(4);
        def.CardTypes.Should().Be(CardType.Planeswalker);
        def.StartingLoyalty.Should().Be(4);
        def.IsLegendary.Should().BeTrue();
        def.Subtypes.Should().Contain("Kaito");
    }

    [Fact]
    public void CardDefinition_Kaito_HasThreeLoyaltyAbilities()
    {
        CardDefinitions.TryGet("Kaito, Bane of Nightmares", out var def).Should().BeTrue();
        def!.LoyaltyAbilities.Should().HaveCount(3);

        def.LoyaltyAbilities![0].LoyaltyCost.Should().Be(1);
        def.LoyaltyAbilities[0].Effect.Should().BeOfType<CreateNinjaEmblemEffect>();

        def.LoyaltyAbilities[1].LoyaltyCost.Should().Be(0);
        def.LoyaltyAbilities[1].Effect.Should().BeOfType<SurveilAndDrawEffect>();

        def.LoyaltyAbilities[2].LoyaltyCost.Should().Be(-2);
        def.LoyaltyAbilities[2].Effect.Should().BeOfType<TapAndStunEffect>();
    }

    [Fact]
    public void CardDefinition_Kaito_HasNinjutsuCost()
    {
        CardDefinitions.TryGet("Kaito, Bane of Nightmares", out var def).Should().BeTrue();
        def!.NinjutsuCost.Should().NotBeNull();
        def.NinjutsuCost!.ConvertedManaCost.Should().Be(3);
    }

    [Fact]
    public void CardDefinition_Kaito_HasCreatureModeEffects()
    {
        CardDefinitions.TryGet("Kaito, Bane of Nightmares", out var def).Should().BeTrue();
        def!.ContinuousEffects.Should().NotBeEmpty();
    }

    [Fact]
    public void CardDefinition_Kaito_HasNinjaSubtype()
    {
        CardDefinitions.TryGet("Kaito, Bane of Nightmares", out var def).Should().BeTrue();
        def!.Subtypes.Should().Contain("Ninja");
    }

    [Fact]
    public void CardDefinition_Kaito_CreatureModeHasBecomeCreatureEffect()
    {
        CardDefinitions.TryGet("Kaito, Bane of Nightmares", out var def).Should().BeTrue();
        def!.ContinuousEffects.Should().Contain(e =>
            e.Type == ContinuousEffectType.BecomeCreature
            && e.ApplyToSelf == true
            && e.SetPower == 3
            && e.SetToughness == 4);
    }

    [Fact]
    public void CardDefinition_Kaito_CreatureModeGrantsHexproof()
    {
        CardDefinitions.TryGet("Kaito, Bane of Nightmares", out var def).Should().BeTrue();
        def!.ContinuousEffects.Should().Contain(e =>
            e.Type == ContinuousEffectType.GrantKeyword
            && e.GrantedKeyword == Keyword.Hexproof);
    }

    [Fact]
    public void GameCard_Create_Kaito_LoadsFromRegistry()
    {
        var card = GameCard.Create("Kaito, Bane of Nightmares");

        card.ManaCost.Should().NotBeNull();
        card.ManaCost!.ConvertedManaCost.Should().Be(4);
        card.CardTypes.Should().Be(CardType.Planeswalker);
        card.IsLegendary.Should().BeTrue();
        card.Subtypes.Should().Contain("Kaito");
        card.Subtypes.Should().Contain("Ninja");
    }
}
