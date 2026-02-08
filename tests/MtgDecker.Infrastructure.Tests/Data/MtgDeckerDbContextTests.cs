using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MtgDecker.Domain.Entities;
using MtgDecker.Domain.Enums;
using MtgDecker.Domain.ValueObjects;

namespace MtgDecker.Infrastructure.Tests.Data;

public class MtgDeckerDbContextTests
{
    [Fact]
    public async Task CanSaveAndRetrieveCard()
    {
        using var context = TestDbContextFactory.Create();
        var card = new Card
        {
            Id = Guid.NewGuid(),
            ScryfallId = Guid.NewGuid().ToString(),
            OracleId = Guid.NewGuid().ToString(),
            Name = "Lightning Bolt",
            ManaCost = "{R}",
            Cmc = 1.0,
            TypeLine = "Instant",
            OracleText = "Lightning Bolt deals 3 damage to any target.",
            Colors = "R",
            ColorIdentity = "R",
            Rarity = "uncommon",
            SetCode = "lea",
            SetName = "Limited Edition Alpha"
        };

        context.Cards.Add(card);
        await context.SaveChangesAsync();

        var loaded = await context.Cards.FindAsync(card.Id);
        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("Lightning Bolt");
        loaded.Cmc.Should().Be(1.0);
    }

    [Fact]
    public async Task CanSaveCardWithFaces()
    {
        using var context = TestDbContextFactory.Create();
        var card = new Card
        {
            Id = Guid.NewGuid(),
            ScryfallId = Guid.NewGuid().ToString(),
            OracleId = Guid.NewGuid().ToString(),
            Name = "Delver of Secrets // Insectile Aberration",
            TypeLine = "Creature — Human Wizard // Creature — Human Insect",
            Rarity = "common",
            SetCode = "isd",
            SetName = "Innistrad",
            Layout = "transform",
            Faces = new List<CardFace>
            {
                new() { Id = Guid.NewGuid(), Name = "Delver of Secrets", TypeLine = "Creature — Human Wizard", ManaCost = "{U}", Power = "1", Toughness = "1" },
                new() { Id = Guid.NewGuid(), Name = "Insectile Aberration", TypeLine = "Creature — Human Insect", Power = "3", Toughness = "2" }
            }
        };

        context.Cards.Add(card);
        await context.SaveChangesAsync();

        var faces = context.CardFaces.Where(f => f.CardId == card.Id).ToList();
        faces.Should().HaveCount(2);
    }

    [Fact]
    public async Task CanSaveCardWithLegalities()
    {
        using var context = TestDbContextFactory.Create();
        var card = new Card
        {
            Id = Guid.NewGuid(),
            ScryfallId = Guid.NewGuid().ToString(),
            OracleId = Guid.NewGuid().ToString(),
            Name = "Lightning Bolt",
            TypeLine = "Instant",
            Rarity = "uncommon",
            SetCode = "lea",
            SetName = "Limited Edition Alpha",
            Legalities = new List<CardLegality>
            {
                new("modern", LegalityStatus.Legal),
                new("vintage", LegalityStatus.Legal),
                new("standard", LegalityStatus.NotLegal)
            }
        };

        context.Cards.Add(card);
        await context.SaveChangesAsync();

        var legalities = context.Set<CardLegality>()
            .Where(l => EF.Property<Guid>(l, "CardId") == card.Id)
            .ToList();
        legalities.Should().HaveCount(3);
    }

    [Fact]
    public async Task CanSaveAndRetrieveDeckWithEntries()
    {
        using var context = TestDbContextFactory.Create();
        var cardId = Guid.NewGuid();

        context.Cards.Add(new Card
        {
            Id = cardId,
            ScryfallId = Guid.NewGuid().ToString(),
            OracleId = Guid.NewGuid().ToString(),
            Name = "Lightning Bolt",
            TypeLine = "Instant",
            Rarity = "uncommon",
            SetCode = "lea",
            SetName = "Limited Edition Alpha"
        });

        var deck = new Deck
        {
            Id = Guid.NewGuid(),
            Name = "Burn",
            Format = Format.Modern,
            UserId = Guid.NewGuid(),
            Entries = new List<DeckEntry>
            {
                new() { Id = Guid.NewGuid(), CardId = cardId, Quantity = 4, Category = DeckCategory.MainDeck }
            }
        };

        context.Decks.Add(deck);
        await context.SaveChangesAsync();

        var loaded = await context.Decks.FindAsync(deck.Id);
        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("Burn");

        var entries = context.DeckEntries.Where(e => e.DeckId == deck.Id).ToList();
        entries.Should().HaveCount(1);
        entries[0].Quantity.Should().Be(4);
    }

    [Fact]
    public async Task CanSaveAndRetrieveCollectionEntry()
    {
        using var context = TestDbContextFactory.Create();
        var cardId = Guid.NewGuid();

        context.Cards.Add(new Card
        {
            Id = cardId,
            ScryfallId = Guid.NewGuid().ToString(),
            OracleId = Guid.NewGuid().ToString(),
            Name = "Lightning Bolt",
            TypeLine = "Instant",
            Rarity = "uncommon",
            SetCode = "lea",
            SetName = "Limited Edition Alpha"
        });

        var entry = new CollectionEntry
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            CardId = cardId,
            Quantity = 3,
            IsFoil = true,
            Condition = CardCondition.NearMint
        };

        context.CollectionEntries.Add(entry);
        await context.SaveChangesAsync();

        var loaded = await context.CollectionEntries.FindAsync(entry.Id);
        loaded.Should().NotBeNull();
        loaded!.Quantity.Should().Be(3);
        loaded.IsFoil.Should().BeTrue();
        loaded.Condition.Should().Be(CardCondition.NearMint);
    }
}
