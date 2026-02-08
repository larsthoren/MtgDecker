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
    public void AddCard_DuplicateCard_IncrementsQuantity()
    {
        var deck = CreateDeck(Format.Modern);
        var card = CreateCard("Lightning Bolt");
        deck.AddCard(card, 2, DeckCategory.MainDeck);

        deck.AddCard(card, 1, DeckCategory.MainDeck);

        deck.Entries.Should().HaveCount(1);
        deck.Entries[0].Quantity.Should().Be(3);
    }

    [Fact]
    public void AddCard_DuplicateExceedsMaxCopies_ThrowsException()
    {
        var deck = CreateDeck(Format.Modern);
        var card = CreateCard("Lightning Bolt");
        deck.AddCard(card, 3, DeckCategory.MainDeck);

        var act = () => deck.AddCard(card, 2, DeckCategory.MainDeck);

        act.Should().Throw<DomainException>()
            .WithMessage("*cannot exceed*");
    }

    [Fact]
    public void UpdateCardQuantity_ValidQuantity_UpdatesEntry()
    {
        var deck = CreateDeck(Format.Modern);
        var card = CreateCard("Lightning Bolt");
        deck.AddCard(card, 2, DeckCategory.MainDeck);

        deck.UpdateCardQuantity(card.Id, DeckCategory.MainDeck, 4);

        deck.Entries[0].Quantity.Should().Be(4);
    }

    [Fact]
    public void RemoveCard_ExistingCard_RemovesEntry()
    {
        var deck = CreateDeck(Format.Modern);
        var card = CreateCard("Lightning Bolt");
        deck.AddCard(card, 4, DeckCategory.MainDeck);

        deck.RemoveCard(card.Id, DeckCategory.MainDeck);

        deck.Entries.Should().BeEmpty();
    }

    [Fact]
    public void RemoveCard_TargetsCorrectCategory()
    {
        var deck = CreateDeck(Format.Modern);
        var card = CreateCard("Lightning Bolt");
        deck.AddCard(card, 4, DeckCategory.MainDeck);
        deck.AddCard(card, 2, DeckCategory.Maybeboard);

        deck.RemoveCard(card.Id, DeckCategory.Maybeboard);

        deck.Entries.Should().HaveCount(1);
        deck.Entries[0].Category.Should().Be(DeckCategory.MainDeck);
        deck.Entries[0].Quantity.Should().Be(4);
    }

    [Fact]
    public void MoveCardCategory_MovesFromMaybeboardToMainDeck()
    {
        var deck = CreateDeck(Format.Modern);
        var card = CreateCard("Lightning Bolt");
        deck.AddCard(card, 2, DeckCategory.Maybeboard);

        deck.MoveCardCategory(card, DeckCategory.Maybeboard, DeckCategory.MainDeck);

        deck.Entries.Should().HaveCount(1);
        deck.Entries[0].Category.Should().Be(DeckCategory.MainDeck);
        deck.Entries[0].Quantity.Should().Be(2);
    }

    [Fact]
    public void MoveCardCategory_MergesWithExistingInTarget()
    {
        var deck = CreateDeck(Format.Modern);
        var card = CreateCard("Lightning Bolt");
        deck.AddCard(card, 2, DeckCategory.MainDeck);
        deck.AddCard(card, 1, DeckCategory.Maybeboard);

        deck.MoveCardCategory(card, DeckCategory.Maybeboard, DeckCategory.MainDeck);

        deck.Entries.Should().HaveCount(1);
        deck.Entries[0].Category.Should().Be(DeckCategory.MainDeck);
        deck.Entries[0].Quantity.Should().Be(3);
    }

    [Fact]
    public void MoveCardCategory_ExceedsCopyLimit_ThrowsWithoutRemoving()
    {
        var deck = CreateDeck(Format.Modern);
        var card = CreateCard("Lightning Bolt");
        deck.AddCard(card, 4, DeckCategory.MainDeck);
        deck.AddCard(card, 1, DeckCategory.Maybeboard);

        var act = () => deck.MoveCardCategory(card, DeckCategory.Maybeboard, DeckCategory.MainDeck);

        act.Should().Throw<DomainException>().WithMessage("*cannot exceed*");
        // Maybeboard entry should still exist
        deck.Entries.Should().HaveCount(2);
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

    [Fact]
    public void AddCard_ToMaybeboard_SkipsCopyLimit()
    {
        var deck = CreateDeck(Format.Modern);
        var card = CreateCard("Lightning Bolt");

        deck.AddCard(card, 10, DeckCategory.Maybeboard);

        deck.Entries.Should().HaveCount(1);
        deck.Entries[0].Quantity.Should().Be(10);
        deck.Entries[0].Category.Should().Be(DeckCategory.Maybeboard);
    }

    [Fact]
    public void AddCard_ToMaybeboard_DoesNotCountInMainDeck()
    {
        var deck = CreateDeck(Format.Modern);
        deck.AddCard(CreateCard("Card A"), 4, DeckCategory.MainDeck);
        deck.AddCard(CreateCard("Card B"), 3, DeckCategory.Maybeboard);

        deck.TotalMainDeckCount.Should().Be(4);
        deck.TotalMaybeboardCount.Should().Be(3);
    }

    [Fact]
    public void AddCard_ToMaybeboard_InCommanderFormat_Succeeds()
    {
        var deck = CreateDeck(Format.Commander);
        var card = CreateCard("Sol Ring");

        deck.AddCard(card, 5, DeckCategory.Maybeboard);

        deck.Entries.Should().HaveCount(1);
    }

    [Fact]
    public void AddCard_SetsUpdatedAtToProvidedTimestamp()
    {
        var deck = CreateDeck(Format.Modern);
        var card = CreateCard("Lightning Bolt");
        var timestamp = new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc);

        deck.AddCard(card, 4, DeckCategory.MainDeck, timestamp);

        deck.UpdatedAt.Should().Be(timestamp);
    }

    [Fact]
    public void UpdateCardQuantity_SetsUpdatedAtToProvidedTimestamp()
    {
        var deck = CreateDeck(Format.Modern);
        var card = CreateCard("Lightning Bolt");
        deck.AddCard(card, 2, DeckCategory.MainDeck);
        var timestamp = new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc);

        deck.UpdateCardQuantity(card.Id, DeckCategory.MainDeck, 4, timestamp);

        deck.UpdatedAt.Should().Be(timestamp);
    }

    [Fact]
    public void RemoveCard_SetsUpdatedAtToProvidedTimestamp()
    {
        var deck = CreateDeck(Format.Modern);
        var card = CreateCard("Lightning Bolt");
        deck.AddCard(card, 4, DeckCategory.MainDeck);
        var timestamp = new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc);

        deck.RemoveCard(card.Id, DeckCategory.MainDeck, timestamp);

        deck.UpdatedAt.Should().Be(timestamp);
    }

    [Fact]
    public void MoveCardCategory_SetsUpdatedAtToProvidedTimestamp()
    {
        var deck = CreateDeck(Format.Modern);
        var card = CreateCard("Lightning Bolt");
        deck.AddCard(card, 2, DeckCategory.Maybeboard);
        var timestamp = new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc);

        deck.MoveCardCategory(card, DeckCategory.Maybeboard, DeckCategory.MainDeck, timestamp);

        deck.UpdatedAt.Should().Be(timestamp);
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
