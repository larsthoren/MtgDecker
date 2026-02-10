using FluentAssertions;
using MtgDecker.Engine;

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
}
