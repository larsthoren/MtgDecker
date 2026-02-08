using FluentAssertions;
using MtgDecker.Domain.Entities;
using MtgDecker.Domain.Enums;
using MtgDecker.Infrastructure.Data;
using MtgDecker.Infrastructure.Data.Repositories;
using MtgDecker.Infrastructure.Tests.Data;

namespace MtgDecker.Infrastructure.Tests.Data.Repositories;

public class DeckRepositoryTests
{
    [Fact]
    public async Task AddAsync_SavesDeck()
    {
        using var context = TestDbContextFactory.Create();
        var repo = new DeckRepository(context);
        var deck = CreateDeck("Burn");

        await repo.AddAsync(deck);

        context.Decks.Count().Should().Be(1);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingDeck_ReturnsDeckWithEntries()
    {
        using var context = TestDbContextFactory.Create();
        var cardId = Guid.NewGuid();
        SeedCard(context, cardId);
        var deck = CreateDeck("Burn");
        deck.Entries.Add(new DeckEntry
        {
            Id = Guid.NewGuid(),
            DeckId = deck.Id,
            CardId = cardId,
            Quantity = 4,
            Category = DeckCategory.MainDeck
        });
        context.Decks.Add(deck);
        await context.SaveChangesAsync();
        var repo = new DeckRepository(context);

        var result = await repo.GetByIdAsync(deck.Id);

        result.Should().NotBeNull();
        result!.Entries.Should().HaveCount(1);
    }

    [Fact]
    public async Task ListByUserAsync_ReturnsOnlyUserDecks()
    {
        using var context = TestDbContextFactory.Create();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        context.Decks.Add(CreateDeck("My Deck 1", userId));
        context.Decks.Add(CreateDeck("My Deck 2", userId));
        context.Decks.Add(CreateDeck("Other Deck", otherUserId));
        await context.SaveChangesAsync();
        var repo = new DeckRepository(context);

        var result = await repo.ListByUserAsync(userId);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesDeck()
    {
        using var context = TestDbContextFactory.Create();
        var deck = CreateDeck("Burn");
        context.Decks.Add(deck);
        await context.SaveChangesAsync();
        var repo = new DeckRepository(context);

        deck.Name = "Updated Burn";
        await repo.UpdateAsync(deck);

        var loaded = await context.Decks.FindAsync(deck.Id);
        loaded!.Name.Should().Be("Updated Burn");
    }

    [Fact]
    public async Task DeleteAsync_RemovesDeck()
    {
        using var context = TestDbContextFactory.Create();
        var deck = CreateDeck("Burn");
        context.Decks.Add(deck);
        await context.SaveChangesAsync();
        var repo = new DeckRepository(context);

        await repo.DeleteAsync(deck.Id);

        context.Decks.Count().Should().Be(0);
    }

    private static void SeedCard(MtgDeckerDbContext context, Guid id)
    {
        context.Cards.Add(new Card
        {
            Id = id,
            ScryfallId = Guid.NewGuid().ToString(),
            OracleId = Guid.NewGuid().ToString(),
            Name = "Test Card",
            TypeLine = "Instant",
            Rarity = "common",
            SetCode = "tst",
            SetName = "Test Set"
        });
        context.SaveChanges();
    }

    private static Deck CreateDeck(string name, Guid? userId = null)
    {
        return new Deck
        {
            Id = Guid.NewGuid(),
            Name = name,
            Format = Format.Modern,
            UserId = userId ?? Guid.NewGuid()
        };
    }
}
