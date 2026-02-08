using FluentAssertions;
using MtgDecker.Domain.Entities;
using MtgDecker.Domain.Enums;
using MtgDecker.Infrastructure.Data;
using MtgDecker.Infrastructure.Data.Repositories;
using MtgDecker.Infrastructure.Tests.Data;

namespace MtgDecker.Infrastructure.Tests.Data.Repositories;

public class CollectionRepositoryTests
{
    [Fact]
    public async Task AddAsync_SavesEntry()
    {
        using var context = TestDbContextFactory.Create();
        var cardId = SeedCard(context);
        var repo = new CollectionRepository(context);
        var entry = CreateEntry(cardId);

        await repo.AddAsync(entry);

        context.CollectionEntries.Count().Should().Be(1);
    }

    [Fact]
    public async Task GetByUserAsync_ReturnsOnlyUserEntries()
    {
        using var context = TestDbContextFactory.Create();
        var cardId = SeedCard(context);
        var userId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        context.CollectionEntries.Add(CreateEntry(cardId, userId));
        context.CollectionEntries.Add(CreateEntry(cardId, userId));
        context.CollectionEntries.Add(CreateEntry(cardId, otherId));
        await context.SaveChangesAsync();
        var repo = new CollectionRepository(context);

        var result = await repo.GetByUserAsync(userId);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task DeleteAsync_RemovesEntry()
    {
        using var context = TestDbContextFactory.Create();
        var cardId = SeedCard(context);
        var entry = CreateEntry(cardId);
        context.CollectionEntries.Add(entry);
        await context.SaveChangesAsync();
        var repo = new CollectionRepository(context);

        await repo.DeleteAsync(entry.Id);

        context.CollectionEntries.Count().Should().Be(0);
    }

    [Fact]
    public async Task SearchAsync_ByCardName_ReturnsMatches()
    {
        using var context = TestDbContextFactory.Create();
        var userId = Guid.NewGuid();
        var boltId = SeedCard(context, "Lightning Bolt");
        var counterId = SeedCard(context, "Counterspell");
        context.CollectionEntries.Add(CreateEntry(boltId, userId));
        context.CollectionEntries.Add(CreateEntry(counterId, userId));
        await context.SaveChangesAsync();
        var repo = new CollectionRepository(context);

        var result = await repo.SearchAsync(userId, "Lightning");

        result.Should().HaveCount(1);
    }

    private static Guid SeedCard(MtgDeckerDbContext context, string name = "Test Card")
    {
        var card = new Card
        {
            Id = Guid.NewGuid(),
            ScryfallId = Guid.NewGuid().ToString(),
            OracleId = Guid.NewGuid().ToString(),
            Name = name,
            TypeLine = "Instant",
            Rarity = "common",
            SetCode = "tst",
            SetName = "Test Set"
        };
        context.Cards.Add(card);
        context.SaveChanges();
        return card.Id;
    }

    private static CollectionEntry CreateEntry(Guid cardId, Guid? userId = null)
    {
        return new CollectionEntry
        {
            Id = Guid.NewGuid(),
            UserId = userId ?? Guid.NewGuid(),
            CardId = cardId,
            Quantity = 4,
            IsFoil = false,
            Condition = CardCondition.NearMint
        };
    }
}
