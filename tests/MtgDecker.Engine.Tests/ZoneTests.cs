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

    [Fact]
    public void PeekTop_ReturnsTopNCards_WithoutRemoving()
    {
        var zone = new Zone(ZoneType.Library);
        var card1 = new GameCard { Name = "Bottom" };
        var card2 = new GameCard { Name = "Middle" };
        var card3 = new GameCard { Name = "Top" };
        zone.Add(card1);
        zone.Add(card2);
        zone.Add(card3);

        var peeked = zone.PeekTop(2);

        peeked.Should().HaveCount(2);
        peeked[0].Name.Should().Be("Top");
        peeked[1].Name.Should().Be("Middle");
        zone.Count.Should().Be(3);
    }

    [Fact]
    public void PeekTop_ReturnsAllCards_WhenCountExceedsSize()
    {
        var zone = new Zone(ZoneType.Library);
        zone.Add(new GameCard { Name = "Only" });

        var peeked = zone.PeekTop(5);

        peeked.Should().HaveCount(1);
        peeked[0].Name.Should().Be("Only");
    }

    [Fact]
    public void PeekTop_EmptyZone_ReturnsEmpty()
    {
        var zone = new Zone(ZoneType.Library);
        var peeked = zone.PeekTop(3);
        peeked.Should().BeEmpty();
    }

    [Fact]
    public void AddToTop_PlacesCardAtTopOfZone()
    {
        var zone = new Zone(ZoneType.Library);
        var card1 = new GameCard { Name = "Card1" };
        var card2 = new GameCard { Name = "Card2" };
        zone.Add(card1);
        zone.AddToTop(card2);
        zone.PeekTop(1)[0].Name.Should().Be("Card2");
    }

    [Fact]
    public void Remove_RemovesSpecificCardObject()
    {
        var zone = new Zone(ZoneType.Hand);
        var card1 = new GameCard { Name = "Card1" };
        var card2 = new GameCard { Name = "Card2" };
        zone.Add(card1);
        zone.Add(card2);
        zone.Remove(card1).Should().BeTrue();
        zone.Count.Should().Be(1);
        zone.Cards[0].Name.Should().Be("Card2");
    }

    [Fact]
    public void Remove_ReturnsFalse_WhenCardNotInZone()
    {
        var zone = new Zone(ZoneType.Hand);
        var card = new GameCard { Name = "NotHere" };
        zone.Remove(card).Should().BeFalse();
    }

    [Fact]
    public void Contains_ReflectsAddAndRemove()
    {
        var zone = new Zone(ZoneType.Battlefield);
        var card = new GameCard { Name = "Test" };

        zone.Contains(card.Id).Should().BeFalse();
        zone.Add(card);
        zone.Contains(card.Id).Should().BeTrue();
        zone.Remove(card);
        zone.Contains(card.Id).Should().BeFalse();
    }

    [Fact]
    public void Contains_ReflectsRemoveById()
    {
        var zone = new Zone(ZoneType.Battlefield);
        var card = new GameCard { Name = "Test" };
        zone.Add(card);

        zone.RemoveById(card.Id);
        zone.Contains(card.Id).Should().BeFalse();
    }

    [Fact]
    public void Contains_ReflectsDrawFromTop()
    {
        var zone = new Zone(ZoneType.Library);
        var card = new GameCard { Name = "Test" };
        zone.Add(card);

        zone.DrawFromTop();
        zone.Contains(card.Id).Should().BeFalse();
    }

    [Fact]
    public void Contains_ReflectsClear()
    {
        var zone = new Zone(ZoneType.Hand);
        var card = new GameCard { Name = "Test" };
        zone.Add(card);

        zone.Clear();
        zone.Contains(card.Id).Should().BeFalse();
    }

    [Fact]
    public void Contains_ReflectsAddRange()
    {
        var zone = new Zone(ZoneType.Battlefield);
        var cards = new[] { new GameCard { Name = "A" }, new GameCard { Name = "B" } };

        zone.AddRange(cards);
        zone.Contains(cards[0].Id).Should().BeTrue();
        zone.Contains(cards[1].Id).Should().BeTrue();
    }

    [Fact]
    public void Contains_ReflectsAddToBottom()
    {
        var zone = new Zone(ZoneType.Library);
        var card = new GameCard { Name = "Test" };

        zone.AddToBottom(card);
        zone.Contains(card.Id).Should().BeTrue();
    }
}
