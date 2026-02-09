using FluentAssertions;
using MtgDecker.Domain.Entities;
using MtgDecker.Domain.Enums;
using MtgDecker.Domain.Services;

namespace MtgDecker.Domain.Tests.Services;

public class ShortageCalculatorTests
{
    [Fact]
    public void Calculate_CardNotOwned_ReturnsFullQuantityAsShortage()
    {
        var oracleId = Guid.NewGuid().ToString();
        var card = CreateCard("Lightning Bolt", oracleId);
        var deck = CreateDeckWithEntry(card, quantity: 4);
        var collection = new List<CollectionEntry>();

        var shortages = ShortageCalculator.Calculate(deck, collection, cardLookup: new[] { card });

        shortages.Should().HaveCount(1);
        shortages[0].CardName.Should().Be("Lightning Bolt");
        shortages[0].Needed.Should().Be(4);
        shortages[0].Owned.Should().Be(0);
        shortages[0].Shortage.Should().Be(4);
    }

    [Fact]
    public void Calculate_CardPartiallyOwned_ReturnsShortage()
    {
        var oracleId = Guid.NewGuid().ToString();
        var card = CreateCard("Lightning Bolt", oracleId);
        var deck = CreateDeckWithEntry(card, quantity: 4);
        var collection = new List<CollectionEntry>
        {
            new() { CardId = card.Id, Quantity = 2, UserId = deck.UserId }
        };

        var shortages = ShortageCalculator.Calculate(deck, collection, cardLookup: new[] { card });

        shortages.Should().HaveCount(1);
        shortages[0].Shortage.Should().Be(2);
    }

    [Fact]
    public void Calculate_CardFullyOwned_ReturnsNoShortage()
    {
        var oracleId = Guid.NewGuid().ToString();
        var card = CreateCard("Lightning Bolt", oracleId);
        var deck = CreateDeckWithEntry(card, quantity: 4);
        var collection = new List<CollectionEntry>
        {
            new() { CardId = card.Id, Quantity = 4, UserId = deck.UserId }
        };

        var shortages = ShortageCalculator.Calculate(deck, collection, cardLookup: new[] { card });

        shortages.Should().BeEmpty();
    }

    [Fact]
    public void Calculate_DifferentPrintingSameOracle_CountsAsOwned()
    {
        var oracleId = Guid.NewGuid().ToString();
        var deckCard = CreateCard("Lightning Bolt", oracleId);
        var ownedCard = CreateCard("Lightning Bolt", oracleId);
        var deck = CreateDeckWithEntry(deckCard, quantity: 4);
        var collection = new List<CollectionEntry>
        {
            new() { CardId = ownedCard.Id, Quantity = 3, UserId = deck.UserId }
        };

        var shortages = ShortageCalculator.Calculate(
            deck, collection, cardLookup: new[] { deckCard, ownedCard });

        shortages.Should().HaveCount(1);
        shortages[0].Shortage.Should().Be(1);
    }

    [Fact]
    public void Calculate_CardInMultipleCategories_AggregatesNeeded()
    {
        // Card appears in MainDeck (qty 4) and Sideboard (qty 2), user owns 3.
        // Total needed = 6, owned = 3, so shortage should be 3 in a single entry.
        var oracleId = Guid.NewGuid().ToString();
        var card = CreateCard("Lightning Bolt", oracleId);
        var deck = CreateDeckWithEntries(card, mainDeckQty: 4, sideboardQty: 2);
        var collection = new List<CollectionEntry>
        {
            new() { CardId = card.Id, Quantity = 3, UserId = deck.UserId }
        };

        var shortages = ShortageCalculator.Calculate(deck, collection, cardLookup: new[] { card });

        shortages.Should().HaveCount(1);
        shortages[0].CardName.Should().Be("Lightning Bolt");
        shortages[0].Needed.Should().Be(6);
        shortages[0].Owned.Should().Be(3);
        shortages[0].Shortage.Should().Be(3);
    }

    [Fact]
    public void Calculate_CardInMultipleCategories_NoShortageWhenOwnedEnough()
    {
        // Card appears in MainDeck (qty 4) and Sideboard (qty 2), user owns 6.
        // Total needed = 6, owned = 6, so no shortage.
        var oracleId = Guid.NewGuid().ToString();
        var card = CreateCard("Lightning Bolt", oracleId);
        var deck = CreateDeckWithEntries(card, mainDeckQty: 4, sideboardQty: 2);
        var collection = new List<CollectionEntry>
        {
            new() { CardId = card.Id, Quantity = 6, UserId = deck.UserId }
        };

        var shortages = ShortageCalculator.Calculate(deck, collection, cardLookup: new[] { card });

        shortages.Should().BeEmpty();
    }

    private static Card CreateCard(string name, string oracleId)
    {
        return new Card
        {
            Id = Guid.NewGuid(),
            ScryfallId = Guid.NewGuid().ToString(),
            OracleId = oracleId,
            Name = name,
            TypeLine = "Instant",
            Rarity = "common",
            SetCode = "tst",
            SetName = "Test Set"
        };
    }

    private static Deck CreateDeckWithEntry(Card card, int quantity)
    {
        var deck = new Deck
        {
            Id = Guid.NewGuid(),
            Name = "Test Deck",
            Format = Format.Modern,
            UserId = Guid.NewGuid()
        };
        deck.AddCard(card, quantity, DeckCategory.MainDeck, DateTime.UtcNow);
        return deck;
    }

    private static Deck CreateDeckWithEntries(Card card, int mainDeckQty, int sideboardQty)
    {
        var deck = new Deck
        {
            Id = Guid.NewGuid(),
            Name = "Test Deck",
            Format = Format.Modern,
            UserId = Guid.NewGuid()
        };
        deck.AddCard(card, mainDeckQty, DeckCategory.MainDeck, DateTime.UtcNow);
        deck.AddCard(card, sideboardQty, DeckCategory.Sideboard, DateTime.UtcNow);
        return deck;
    }
}
