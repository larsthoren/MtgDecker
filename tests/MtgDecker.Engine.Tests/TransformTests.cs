using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine.Tests;

public class TransformTests
{
    [Fact]
    public void GameCard_IsTransformed_DefaultsFalse()
    {
        var card = new GameCard { Name = "Delver of Secrets" };

        card.IsTransformed.Should().BeFalse();
    }

    [Fact]
    public void GameCard_BackFaceDefinition_DefaultsNull()
    {
        var card = new GameCard { Name = "Delver of Secrets" };

        card.BackFaceDefinition.Should().BeNull();
    }

    [Fact]
    public void GameCard_WhenTransformed_NameFromBackFace()
    {
        var backFace = new CardDefinition(
            ManaCost: null,
            ManaAbility: null,
            Power: 3,
            Toughness: 2,
            CardTypes: CardType.Creature)
        {
            Name = "Insectile Aberration",
        };

        var card = new GameCard
        {
            Name = "Delver of Secrets",
            TypeLine = "Creature — Human Wizard",
            BackFaceDefinition = backFace,
        };

        card.IsTransformed = true;

        card.Name.Should().Be("Insectile Aberration");
    }

    [Fact]
    public void GameCard_WhenTransformed_CardTypesFromBackFace()
    {
        var backFace = new CardDefinition(
            ManaCost: null,
            ManaAbility: null,
            Power: null,
            Toughness: null,
            CardTypes: CardType.Land)
        {
            Name = "Search for Azcanta",
        };

        var card = new GameCard
        {
            Name = "Search for Azcanta",
            TypeLine = "Legendary Enchantment",
            CardTypes = CardType.Enchantment,
            BackFaceDefinition = backFace,
        };

        card.IsTransformed = true;

        card.CardTypes.Should().Be(CardType.Land);
    }

    [Fact]
    public void GameCard_WhenTransformed_PowerToughnessFromBackFace()
    {
        var backFace = new CardDefinition(
            ManaCost: null,
            ManaAbility: null,
            Power: 3,
            Toughness: 2,
            CardTypes: CardType.Creature)
        {
            Name = "Insectile Aberration",
        };

        var card = new GameCard
        {
            Name = "Delver of Secrets",
            TypeLine = "Creature — Human Wizard",
            BasePower = 1,
            BaseToughness = 1,
            BackFaceDefinition = backFace,
        };

        card.IsTransformed = true;

        card.BasePower.Should().Be(3);
        card.BaseToughness.Should().Be(2);
    }

    [Fact]
    public void GameCard_WhenNotTransformed_UsesOwnProperties()
    {
        var backFace = new CardDefinition(
            ManaCost: null,
            ManaAbility: null,
            Power: 3,
            Toughness: 2,
            CardTypes: CardType.Creature)
        {
            Name = "Insectile Aberration",
        };

        var card = new GameCard
        {
            Name = "Delver of Secrets",
            TypeLine = "Creature — Human Wizard",
            BasePower = 1,
            BaseToughness = 1,
            CardTypes = CardType.Creature,
            BackFaceDefinition = backFace,
        };

        // Not transformed — should use front face values
        card.Name.Should().Be("Delver of Secrets");
        card.CardTypes.Should().Be(CardType.Creature);
        card.BasePower.Should().Be(1);
        card.BaseToughness.Should().Be(1);
    }

    [Fact]
    public void GameCard_FrontName_AlwaysReturnsFrontFaceName()
    {
        var backFace = new CardDefinition(
            ManaCost: null,
            ManaAbility: null,
            Power: 3,
            Toughness: 2,
            CardTypes: CardType.Creature)
        {
            Name = "Insectile Aberration",
        };

        var card = new GameCard
        {
            Name = "Delver of Secrets",
            BackFaceDefinition = backFace,
        };

        // Before transform
        card.FrontName.Should().Be("Delver of Secrets");

        // After transform — FrontName still returns front
        card.IsTransformed = true;
        card.FrontName.Should().Be("Delver of Secrets");
        card.Name.Should().Be("Insectile Aberration");
    }

    [Fact]
    public void GameCard_WhenTransformed_WithoutBackFace_UsesOwnProperties()
    {
        var card = new GameCard
        {
            Name = "Some Card",
            BasePower = 2,
            BaseToughness = 3,
            CardTypes = CardType.Creature,
        };

        // Setting IsTransformed without BackFaceDefinition shouldn't break anything
        card.IsTransformed = true;

        card.Name.Should().Be("Some Card");
        card.BasePower.Should().Be(2);
        card.BaseToughness.Should().Be(3);
        card.CardTypes.Should().Be(CardType.Creature);
    }
}
