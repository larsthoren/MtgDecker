using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Tests;

public class ZoneTests
{
    [Fact]
    public void Constructor_SetsType()
    {
        var zone = new Zone(ZoneType.Hand);
        zone.Type.Should().Be(ZoneType.Hand);
    }

    [Fact]
    public void Constructor_StartsEmpty()
    {
        var zone = new Zone(ZoneType.Hand);
        zone.Count.Should().Be(0);
        zone.Cards.Should().BeEmpty();
    }

    [Fact]
    public void Add_AddsCardToEnd()
    {
        var zone = new Zone(ZoneType.Hand);
        var card = new GameCard { Name = "Forest" };

        zone.Add(card);

        zone.Count.Should().Be(1);
        zone.Cards[0].Should().BeSameAs(card);
    }

    [Fact]
    public void AddToBottom_InsertsAtBeginning()
    {
        var zone = new Zone(ZoneType.Library);
        var first = new GameCard { Name = "Forest" };
        var bottom = new GameCard { Name = "Mountain" };

        zone.Add(first);
        zone.AddToBottom(bottom);

        zone.Cards[0].Should().BeSameAs(bottom);
        zone.Cards[1].Should().BeSameAs(first);
    }

    [Fact]
    public void AddRange_AddsMultipleCards()
    {
        var zone = new Zone(ZoneType.Library);
        var cards = new[] { new GameCard { Name = "A" }, new GameCard { Name = "B" } };

        zone.AddRange(cards);

        zone.Count.Should().Be(2);
    }

    [Fact]
    public void RemoveById_RemovesAndReturnsCard()
    {
        var zone = new Zone(ZoneType.Hand);
        var card = new GameCard { Name = "Forest" };
        zone.Add(card);

        var removed = zone.RemoveById(card.Id);

        removed.Should().BeSameAs(card);
        zone.Count.Should().Be(0);
    }

    [Fact]
    public void RemoveById_ReturnsNull_WhenNotFound()
    {
        var zone = new Zone(ZoneType.Hand);

        var removed = zone.RemoveById(Guid.NewGuid());

        removed.Should().BeNull();
    }

    [Fact]
    public void DrawFromTop_RemovesAndReturnsLastCard()
    {
        var zone = new Zone(ZoneType.Library);
        var bottom = new GameCard { Name = "Bottom" };
        var top = new GameCard { Name = "Top" };
        zone.Add(bottom);
        zone.Add(top);

        var drawn = zone.DrawFromTop();

        drawn.Should().BeSameAs(top);
        zone.Count.Should().Be(1);
        zone.Cards[0].Should().BeSameAs(bottom);
    }

    [Fact]
    public void DrawFromTop_ReturnsNull_WhenEmpty()
    {
        var zone = new Zone(ZoneType.Library);

        var drawn = zone.DrawFromTop();

        drawn.Should().BeNull();
    }

    [Fact]
    public void Contains_ReturnsTrueForExistingCard()
    {
        var zone = new Zone(ZoneType.Hand);
        var card = new GameCard { Name = "Forest" };
        zone.Add(card);

        zone.Contains(card.Id).Should().BeTrue();
    }

    [Fact]
    public void Contains_ReturnsFalseForMissingCard()
    {
        var zone = new Zone(ZoneType.Hand);

        zone.Contains(Guid.NewGuid()).Should().BeFalse();
    }

    [Fact]
    public void Clear_RemovesAllCards()
    {
        var zone = new Zone(ZoneType.Hand);
        zone.Add(new GameCard { Name = "A" });
        zone.Add(new GameCard { Name = "B" });

        zone.Clear();

        zone.Count.Should().Be(0);
    }

    [Fact]
    public void Shuffle_PreservesAllCards()
    {
        var zone = new Zone(ZoneType.Library);
        var cards = Enumerable.Range(0, 20)
            .Select(i => new GameCard { Name = $"Card{i}" })
            .ToList();
        zone.AddRange(cards);

        zone.Shuffle();

        zone.Count.Should().Be(20);
        foreach (var card in cards)
            zone.Contains(card.Id).Should().BeTrue();
    }
}
