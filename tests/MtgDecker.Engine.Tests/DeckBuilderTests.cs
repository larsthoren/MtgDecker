using FluentAssertions;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class DeckBuilderTests
{
    [Fact]
    public void AddCard_CreatesCardsWithCorrectProperties()
    {
        var deck = new DeckBuilder()
            .AddCard("Grizzly Bears", 4, "Creature — Bear")
            .Build();

        deck.Should().HaveCount(4);
        deck.Should().AllSatisfy(c =>
        {
            c.Name.Should().Be("Grizzly Bears");
            c.TypeLine.Should().Be("Creature — Bear");
        });
    }

    [Fact]
    public void AddCard_EachCardHasUniqueId()
    {
        var deck = new DeckBuilder()
            .AddCard("Forest", 3)
            .Build();

        deck.Select(c => c.Id).Distinct().Should().HaveCount(3);
    }

    [Fact]
    public void AddLand_SetsBasicLandTypeLine()
    {
        var deck = new DeckBuilder()
            .AddLand("Forest", 2)
            .Build();

        deck.Should().HaveCount(2);
        deck.Should().AllSatisfy(c =>
        {
            c.Name.Should().Be("Forest");
            c.IsLand.Should().BeTrue();
        });
    }

    [Fact]
    public void Build_CombinesMultipleAddCalls()
    {
        var deck = new DeckBuilder()
            .AddLand("Forest", 20)
            .AddCard("Grizzly Bears", 40, "Creature — Bear")
            .Build();

        deck.Should().HaveCount(60);
        deck.Count(c => c.IsLand).Should().Be(20);
        deck.Count(c => c.IsCreature).Should().Be(40);
    }
}
