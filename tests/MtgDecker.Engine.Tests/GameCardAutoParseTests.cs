using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Triggers;

namespace MtgDecker.Engine.Tests;

public class GameCardAutoParseTests
{
    [Fact]
    public void Create_WithManaCostString_ParsesManaCost()
    {
        var card = GameCard.Create("Grizzly Bears", "Creature — Bear",
            imageUrl: null, manaCost: "{1}{G}", power: "2", toughness: "2");

        card.ManaCost.Should().NotBeNull();
        card.ManaCost!.ConvertedManaCost.Should().Be(2);
        card.ManaCost.ColorRequirements[ManaColor.Green].Should().Be(1);
        card.ManaCost.GenericCost.Should().Be(1);
    }

    [Fact]
    public void Create_WithPowerToughness_ParsesIntegers()
    {
        var card = GameCard.Create("Grizzly Bears", "Creature — Bear",
            imageUrl: null, manaCost: "{1}{G}", power: "2", toughness: "2");

        card.Power.Should().Be(2);
        card.Toughness.Should().Be(2);
    }

    [Fact]
    public void Create_WithNonNumericPower_SetsNull()
    {
        var card = GameCard.Create("Tarmogoyf", "Creature — Lhurgoyf",
            imageUrl: null, manaCost: "{1}{G}", power: "*", toughness: "1+*");

        card.Power.Should().BeNull();
        card.Toughness.Should().BeNull();
    }

    [Fact]
    public void Create_WithTypeLine_ParsesCardType()
    {
        var card = GameCard.Create("Grizzly Bears", "Creature — Bear",
            imageUrl: null, manaCost: "{1}{G}", power: "2", toughness: "2");

        card.CardTypes.Should().Be(CardType.Creature);
    }

    [Fact]
    public void Create_BasicLand_AutoDetectsManaAbility()
    {
        var card = GameCard.Create("Mountain", "Basic Land — Mountain",
            imageUrl: null, manaCost: null, power: null, toughness: null);

        card.ManaAbility.Should().NotBeNull();
        card.ManaAbility!.Type.Should().Be(ManaAbilityType.Fixed);
        card.ManaAbility.FixedColor.Should().Be(ManaColor.Red);
        card.CardTypes.Should().Be(CardType.Land);
    }

    [Fact]
    public void Create_RegistryCardOverridesAutoParse()
    {
        // Goblin Lackey is in CardDefinitions — its registry data should take precedence
        var card = GameCard.Create("Goblin Lackey", "Creature — Goblin",
            imageUrl: null, manaCost: "{R}", power: "1", toughness: "1");

        card.ManaCost.Should().NotBeNull();
        card.Power.Should().Be(1);
        card.Toughness.Should().Be(1);
    }

    [Fact]
    public void Create_InstantSorcery_HasNoManaAbility()
    {
        var card = GameCard.Create("Lightning Bolt", "Instant",
            imageUrl: null, manaCost: "{R}", power: null, toughness: null);

        card.ManaAbility.Should().BeNull();
        card.CardTypes.Should().Be(CardType.Instant);
    }

    [Fact]
    public void Create_LegacyOverload_StillWorks()
    {
        // The old 3-parameter overload should still work
        var card = GameCard.Create("Mountain", "Basic Land — Mountain");

        card.Name.Should().Be("Mountain");
        card.TypeLine.Should().Be("Basic Land — Mountain");
    }

    [Fact]
    public void Create_AutoParse_ExtractsSubtypes()
    {
        var card = GameCard.Create("Test Goblin", "Creature — Goblin Warrior", null, "{R}", "2", "2");
        card.Subtypes.Should().BeEquivalentTo("Goblin", "Warrior");
    }

    [Fact]
    public void Create_AutoParse_NoSubtypes_ReturnsEmpty()
    {
        var card = GameCard.Create("Test Spell", "Instant", null, "{R}", null, null);
        card.Subtypes.Should().BeEmpty();
    }

    [Fact]
    public void Create_Registry_GetsSubtypes_WhenDefined()
    {
        var card = GameCard.Create("Mountain");
        card.Subtypes.Should().Contain("Mountain");
    }

    [Fact]
    public void IsToken_DefaultsFalse()
    {
        var card = GameCard.Create("Test", "Creature", null);
        card.IsToken.Should().BeFalse();
    }

    [Fact]
    public void IsToken_CanBeSetTrue()
    {
        var card = new GameCard { Name = "Goblin Token", IsToken = true };
        card.IsToken.Should().BeTrue();
    }

    [Fact]
    public void Create_Registry_GetsSubtypesAndTriggersFromDefinition()
    {
        var card = GameCard.Create("Goblin Lackey");
        card.Subtypes.Should().NotBeNull();
        card.Triggers.Should().NotBeNull();
    }
}
