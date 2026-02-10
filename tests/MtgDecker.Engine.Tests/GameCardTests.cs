using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine.Tests;

public class GameCardTests
{
    [Fact]
    public void Constructor_GeneratesUniqueId()
    {
        var card1 = new GameCard { Name = "Forest" };
        var card2 = new GameCard { Name = "Forest" };

        card1.Id.Should().NotBe(Guid.Empty);
        card1.Id.Should().NotBe(card2.Id);
    }

    [Fact]
    public void Properties_SetViaInitializer()
    {
        var card = new GameCard
        {
            Name = "Lightning Bolt",
            TypeLine = "Instant",
            ImageUrl = "https://example.com/bolt.jpg"
        };

        card.Name.Should().Be("Lightning Bolt");
        card.TypeLine.Should().Be("Instant");
        card.ImageUrl.Should().Be("https://example.com/bolt.jpg");
    }

    [Fact]
    public void IsTapped_DefaultsFalse()
    {
        var card = new GameCard { Name = "Forest" };
        card.IsTapped.Should().BeFalse();
    }

    [Fact]
    public void IsTapped_CanBeSet()
    {
        var card = new GameCard { Name = "Forest" };
        card.IsTapped = true;
        card.IsTapped.Should().BeTrue();
    }

    [Theory]
    [InlineData("Basic Land — Forest", true)]
    [InlineData("Land — Urza's Tower", true)]
    [InlineData("Creature — Elf Warrior", false)]
    [InlineData("Instant", false)]
    public void IsLand_DetectsLandTypeLine(string typeLine, bool expected)
    {
        var card = new GameCard { Name = "Test", TypeLine = typeLine };
        card.IsLand.Should().Be(expected);
    }

    [Theory]
    [InlineData("Creature — Human Wizard", true)]
    [InlineData("Legendary Creature — Elf", true)]
    [InlineData("Artifact Creature — Golem", true)]
    [InlineData("Instant", false)]
    [InlineData("Basic Land — Forest", false)]
    public void IsCreature_DetectsCreatureTypeLine(string typeLine, bool expected)
    {
        var card = new GameCard { Name = "Test", TypeLine = typeLine };
        card.IsCreature.Should().Be(expected);
    }

    [Fact]
    public void Create_KnownCard_ResolvesManaCost()
    {
        var card = GameCard.Create("Goblin Lackey", "Creature — Goblin");

        card.ManaCost.Should().NotBeNull();
        card.ManaCost!.ConvertedManaCost.Should().Be(1);
        card.ManaCost.ColorRequirements.Should().ContainKey(ManaColor.Red).WhoseValue.Should().Be(1);
    }

    [Fact]
    public void Create_KnownCard_ResolvesManaAbility()
    {
        var card = GameCard.Create("Mountain", "Basic Land — Mountain");

        card.ManaAbility.Should().NotBeNull();
        card.ManaAbility!.Type.Should().Be(ManaAbilityType.Fixed);
        card.ManaAbility.FixedColor.Should().Be(ManaColor.Red);
    }

    [Fact]
    public void Create_KnownCard_ResolvesPowerToughness()
    {
        var card = GameCard.Create("Goblin Lackey", "Creature — Goblin");

        card.Power.Should().Be(1);
        card.Toughness.Should().Be(1);
    }

    [Fact]
    public void Create_KnownCard_ResolvesCardTypes()
    {
        var card = GameCard.Create("Mountain", "Basic Land — Mountain");

        card.CardTypes.Should().HaveFlag(CardType.Land);
    }

    [Fact]
    public void Create_UnknownCard_LeavesPropertiesNull()
    {
        var card = GameCard.Create("Totally Unknown Card", "Artifact");

        card.ManaCost.Should().BeNull();
        card.ManaAbility.Should().BeNull();
        card.Power.Should().BeNull();
        card.Toughness.Should().BeNull();
    }

    [Fact]
    public void Create_UnknownCard_CardTypesIsNone()
    {
        var card = GameCard.Create("Totally Unknown Card", "Artifact");

        card.CardTypes.Should().Be(CardType.None);
    }

    [Fact]
    public void IsLand_TrueForRegisteredLand()
    {
        var card = GameCard.Create("Mountain", "Basic Land — Mountain");

        card.IsLand.Should().BeTrue();
    }

    [Fact]
    public void IsLand_TrueForTypeLine_BackwardCompat()
    {
        var card = new GameCard { Name = "Some Custom Land", TypeLine = "Land" };

        card.IsLand.Should().BeTrue();
    }
}
