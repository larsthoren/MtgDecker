using FluentAssertions;
using MtgDecker.Domain.Entities;
using MtgDecker.Domain.Enums;
using MtgDecker.Domain.Exceptions;

namespace MtgDecker.Domain.Tests.Entities;

public class DeckTests
{
    [Fact]
    public void AddCard_ValidCard_AddsEntry()
    {
        var deck = CreateDeck(Format.Modern);
        var card = CreateCard("Lightning Bolt");

        deck.AddCard(card, 4, DeckCategory.MainDeck);

        deck.Entries.Should().HaveCount(1);
        deck.Entries[0].Quantity.Should().Be(4);
    }

    [Fact]
    public void AddCard_ExceedsMaxCopies_ThrowsException()
    {
        var deck = CreateDeck(Format.Modern);
        var card = CreateCard("Lightning Bolt");

        var act = () => deck.AddCard(card, 5, DeckCategory.MainDeck);

        act.Should().Throw<DomainException>()
            .WithMessage("*cannot exceed 4 copies*");
    }

    [Fact]
    public void AddCard_BasicLandExceedsMaxCopies_Succeeds()
    {
        var deck = CreateDeck(Format.Modern);
        var card = CreateCard("Mountain", typeLine: "Basic Land — Mountain");

        deck.AddCard(card, 20, DeckCategory.MainDeck);

        deck.Entries.Should().HaveCount(1);
        deck.Entries[0].Quantity.Should().Be(20);
    }

    [Fact]
    public void AddCard_ToSideboardInFormatWithNoSideboard_ThrowsException()
    {
        var deck = CreateDeck(Format.Commander);
        var card = CreateCard("Sol Ring");

        var act = () => deck.AddCard(card, 1, DeckCategory.Sideboard);

        act.Should().Throw<DomainException>()
            .WithMessage("*does not allow a sideboard*");
    }

    [Fact]
    public void AddCard_DuplicateCard_ThrowsException()
    {
        var deck = CreateDeck(Format.Modern);
        var card = CreateCard("Lightning Bolt");
        deck.AddCard(card, 2, DeckCategory.MainDeck);

        var act = () => deck.AddCard(card, 2, DeckCategory.MainDeck);

        act.Should().Throw<DomainException>()
            .WithMessage("*already in the deck*");
    }

    [Fact]
    public void UpdateCardQuantity_ValidQuantity_UpdatesEntry()
    {
        var deck = CreateDeck(Format.Modern);
        var card = CreateCard("Lightning Bolt");
        deck.AddCard(card, 2, DeckCategory.MainDeck);

        deck.UpdateCardQuantity(card.Id, 4);

        deck.Entries[0].Quantity.Should().Be(4);
    }

    [Fact]
    public void RemoveCard_ExistingCard_RemovesEntry()
    {
        var deck = CreateDeck(Format.Modern);
        var card = CreateCard("Lightning Bolt");
        deck.AddCard(card, 4, DeckCategory.MainDeck);

        deck.RemoveCard(card.Id);

        deck.Entries.Should().BeEmpty();
    }

    [Fact]
    public void TotalCardCount_ReturnsSumOfAllEntries()
    {
        var deck = CreateDeck(Format.Modern);
        deck.AddCard(CreateCard("Card A"), 4, DeckCategory.MainDeck);
        deck.AddCard(CreateCard("Card B"), 3, DeckCategory.MainDeck);
        deck.AddCard(CreateCard("Card C"), 2, DeckCategory.Sideboard);

        deck.TotalMainDeckCount.Should().Be(7);
        deck.TotalSideboardCount.Should().Be(2);
    }

    [Fact]
    public void AddCard_ZeroQuantity_ThrowsException()
    {
        var deck = CreateDeck(Format.Modern);
        var card = CreateCard("Lightning Bolt");

        var act = () => deck.AddCard(card, 0, DeckCategory.MainDeck);

        act.Should().Throw<DomainException>()
            .WithMessage("*must be at least 1*");
    }

    private static Deck CreateDeck(Format format)
    {
        return new Deck
        {
            Id = Guid.NewGuid(),
            Name = "Test Deck",
            Format = format,
            UserId = Guid.NewGuid()
        };
    }

    private static Card CreateCard(string name, string typeLine = "Instant")
    {
        return new Card
        {
            Id = Guid.NewGuid(),
            ScryfallId = Guid.NewGuid().ToString(),
            OracleId = Guid.NewGuid().ToString(),
            Name = name,
            TypeLine = typeLine,
            Rarity = "common",
            SetCode = "tst",
            SetName = "Test Set"
        };
    }
}
