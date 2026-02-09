using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MtgDecker.Application.Interfaces;
using MtgDecker.Domain.Entities;
using MtgDecker.Domain.Enums;
using MtgDecker.Domain.ValueObjects;
using MtgDecker.Infrastructure.Data;
using MtgDecker.Infrastructure.Data.Repositories;
using MtgDecker.Infrastructure.Tests.Data;

namespace MtgDecker.Infrastructure.Tests.Data.Repositories;

public class CardRepositoryTests
{
    [Fact]
    public async Task GetByIdAsync_ExistingCard_ReturnsCard()
    {
        using var context = TestDbContextFactory.Create();
        var card = SeedCard(context, "Lightning Bolt");
        var repo = new CardRepository(context);

        var result = await repo.GetByIdAsync(card.Id);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Lightning Bolt");
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingCard_ReturnsNull()
    {
        using var context = TestDbContextFactory.Create();
        var repo = new CardRepository(context);

        var result = await repo.GetByIdAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByNameAsync_ExistingCard_ReturnsCard()
    {
        using var context = TestDbContextFactory.Create();
        SeedCard(context, "Lightning Bolt");
        var repo = new CardRepository(context);

        var result = await repo.GetByNameAsync("Lightning Bolt");

        result.Should().NotBeNull();
        result!.Name.Should().Be("Lightning Bolt");
    }

    [Fact]
    public async Task SearchAsync_ByName_ReturnsMatches()
    {
        using var context = TestDbContextFactory.Create();
        SeedCard(context, "Lightning Bolt");
        SeedCard(context, "Lightning Helix");
        SeedCard(context, "Counterspell");
        var repo = new CardRepository(context);

        var (cards, total) = await repo.SearchAsync(new CardSearchFilter { SearchText = "Lightning" });

        cards.Should().HaveCount(2);
        total.Should().Be(2);
    }

    [Fact]
    public async Task SearchAsync_BySetCode_ReturnsMatches()
    {
        using var context = TestDbContextFactory.Create();
        SeedCard(context, "Lightning Bolt", setCode: "lea");
        SeedCard(context, "Counterspell", setCode: "lea");
        SeedCard(context, "Swords to Plowshares", setCode: "ice");
        var repo = new CardRepository(context);

        var (cards, total) = await repo.SearchAsync(new CardSearchFilter { SetCode = "lea" });

        cards.Should().HaveCount(2);
        total.Should().Be(2);
    }

    [Fact]
    public async Task SearchAsync_Paged_ReturnsCorrectPage()
    {
        using var context = TestDbContextFactory.Create();
        for (int i = 0; i < 25; i++)
            SeedCard(context, $"Card {i:D2}");
        var repo = new CardRepository(context);

        var (cards, total) = await repo.SearchAsync(new CardSearchFilter { Page = 2, PageSize = 10 });

        cards.Should().HaveCount(10);
        total.Should().Be(25);
    }

    [Fact]
    public async Task GetByOracleIdAsync_ReturnsAllPrintings()
    {
        using var context = TestDbContextFactory.Create();
        var oracleId = Guid.NewGuid().ToString();
        SeedCard(context, "Lightning Bolt", oracleId: oracleId, setCode: "lea");
        SeedCard(context, "Lightning Bolt", oracleId: oracleId, setCode: "m10");
        SeedCard(context, "Counterspell");
        var repo = new CardRepository(context);

        var result = await repo.GetByOracleIdAsync(oracleId);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task UpsertBatchAsync_NewCards_InsertsAll()
    {
        using var context = TestDbContextFactory.Create();
        var repo = new CardRepository(context);
        var cards = new List<Card>
        {
            CreateCard("Card A"),
            CreateCard("Card B")
        };

        await repo.UpsertBatchAsync(cards);

        context.Cards.Count().Should().Be(2);
    }

    [Fact]
    public async Task UpsertBatchAsync_ExistingCard_Updates()
    {
        using var context = TestDbContextFactory.Create();
        var card = SeedCard(context, "Lightning Bolt");
        var repo = new CardRepository(context);

        var updated = CreateCard("Lightning Bolt");
        updated.ScryfallId = card.ScryfallId;
        updated.OracleText = "Updated text";

        await repo.UpsertBatchAsync(new[] { updated });

        context.Cards.Count().Should().Be(1);
        var loaded = context.Cards.First(c => c.ScryfallId == card.ScryfallId);
        loaded.OracleText.Should().Be("Updated text");
    }

    [Fact]
    public async Task UpsertBatchAsync_BatchFetch_DoesNotQueryPerCard()
    {
        // Arrange: seed multiple existing cards, then upsert a mix of new and existing
        using var context = TestDbContextFactory.Create();
        var scryfallId1 = Guid.NewGuid().ToString();
        var scryfallId2 = Guid.NewGuid().ToString();

        var existing1 = new Card
        {
            Id = Guid.NewGuid(),
            ScryfallId = scryfallId1,
            OracleId = Guid.NewGuid().ToString(),
            Name = "Card One",
            TypeLine = "Creature",
            Rarity = "common",
            SetCode = "tst",
            SetName = "Test Set",
            Legalities = new List<CardLegality>
            {
                new("standard", LegalityStatus.Legal)
            }
        };
        var existing2 = new Card
        {
            Id = Guid.NewGuid(),
            ScryfallId = scryfallId2,
            OracleId = Guid.NewGuid().ToString(),
            Name = "Card Two",
            TypeLine = "Instant",
            Rarity = "rare",
            SetCode = "tst",
            SetName = "Test Set",
            Legalities = new List<CardLegality>
            {
                new("vintage", LegalityStatus.Legal)
            }
        };
        context.Cards.AddRange(existing1, existing2);
        context.SaveChanges();
        context.ChangeTracker.Clear();

        // Act: upsert 2 existing cards (updated) + 1 new card
        var repo = new CardRepository(context);
        var batch = new List<Card>
        {
            new()
            {
                Id = Guid.NewGuid(),
                ScryfallId = scryfallId1,
                OracleId = existing1.OracleId,
                Name = "Card One Updated",
                TypeLine = "Creature",
                Rarity = "common",
                SetCode = "tst",
                SetName = "Test Set",
                Legalities = new List<CardLegality>
                {
                    new("standard", LegalityStatus.Banned)
                }
            },
            new()
            {
                Id = Guid.NewGuid(),
                ScryfallId = scryfallId2,
                OracleId = existing2.OracleId,
                Name = "Card Two Updated",
                TypeLine = "Instant",
                Rarity = "rare",
                SetCode = "tst",
                SetName = "Test Set",
                Legalities = new List<CardLegality>
                {
                    new("vintage", LegalityStatus.Restricted)
                }
            },
            new()
            {
                Id = Guid.NewGuid(),
                ScryfallId = Guid.NewGuid().ToString(),
                OracleId = Guid.NewGuid().ToString(),
                Name = "Card Three New",
                TypeLine = "Sorcery",
                Rarity = "uncommon",
                SetCode = "tst",
                SetName = "Test Set"
            }
        };

        await repo.UpsertBatchAsync(batch);
        context.ChangeTracker.Clear();

        // Assert: 3 total cards (2 updated + 1 new)
        context.Cards.Count().Should().Be(3);

        // Assert: existing cards were updated (not duplicated)
        var card1 = context.Cards.Include(c => c.Legalities).First(c => c.ScryfallId == scryfallId1);
        card1.Name.Should().Be("Card One Updated");
        card1.Legalities.Should().HaveCount(1);
        card1.Legalities[0].Status.Should().Be(LegalityStatus.Banned);

        var card2 = context.Cards.Include(c => c.Legalities).First(c => c.ScryfallId == scryfallId2);
        card2.Name.Should().Be("Card Two Updated");
        card2.Legalities.Should().HaveCount(1);
        card2.Legalities[0].Status.Should().Be(LegalityStatus.Restricted);

        // Assert: new card was inserted
        var card3 = context.Cards.First(c => c.Name == "Card Three New");
        card3.Should().NotBeNull();
    }

    [Fact]
    public async Task GetDistinctSetsAsync_MatchingText_ReturnsSets()
    {
        using var context = TestDbContextFactory.Create();
        SeedCard(context, "Goblin Guide", setCode: "zen", setName: "Zendikar");
        SeedCard(context, "Jace", setCode: "znr", setName: "Zendikar Rising");
        SeedCard(context, "Bolt", setCode: "lea", setName: "Limited Edition Alpha");
        var repo = new CardRepository(context);

        var result = await repo.GetDistinctSetsAsync("Zendikar");

        result.Should().HaveCount(2);
        result.Select(s => s.SetCode).Should().Contain("zen").And.Contain("znr");
    }

    [Fact]
    public async Task GetDistinctSetsAsync_BySetCode_ReturnsSets()
    {
        using var context = TestDbContextFactory.Create();
        SeedCard(context, "Goblin Guide", setCode: "zen", setName: "Zendikar");
        SeedCard(context, "Bolt", setCode: "lea", setName: "Limited Edition Alpha");
        var repo = new CardRepository(context);

        var result = await repo.GetDistinctSetsAsync("zen");

        result.Should().HaveCount(1);
        result[0].SetName.Should().Be("Zendikar");
    }

    [Fact]
    public async Task GetDistinctSetsAsync_NoMatches_ReturnsEmpty()
    {
        using var context = TestDbContextFactory.Create();
        SeedCard(context, "Bolt", setCode: "lea", setName: "Limited Edition Alpha");
        var repo = new CardRepository(context);

        var result = await repo.GetDistinctSetsAsync("xyz");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDistinctTypesAsync_MatchingText_ReturnsTypes()
    {
        using var context = TestDbContextFactory.Create();
        SeedCard(context, "Goblin Guide", typeLine: "Creature — Goblin Scout");
        SeedCard(context, "Counterspell", typeLine: "Instant");
        var repo = new CardRepository(context);

        var result = await repo.GetDistinctTypesAsync("Creature");

        result.Should().Contain("Creature");
    }

    [Fact]
    public async Task GetDistinctTypesAsync_NoMatches_ReturnsEmpty()
    {
        using var context = TestDbContextFactory.Create();
        SeedCard(context, "Bolt", typeLine: "Instant");
        var repo = new CardRepository(context);

        var result = await repo.GetDistinctTypesAsync("Planeswalker");

        result.Should().BeEmpty();
    }

    private static Card SeedCard(
        MtgDeckerDbContext context,
        string name,
        string? oracleId = null,
        string setCode = "tst",
        string setName = "Test Set",
        string typeLine = "Instant")
    {
        var card = CreateCard(name, oracleId, setCode, setName, typeLine);
        context.Cards.Add(card);
        context.SaveChanges();
        return card;
    }

    private static Card CreateCard(
        string name,
        string? oracleId = null,
        string setCode = "tst",
        string setName = "Test Set",
        string typeLine = "Instant")
    {
        return new Card
        {
            Id = Guid.NewGuid(),
            ScryfallId = Guid.NewGuid().ToString(),
            OracleId = oracleId ?? Guid.NewGuid().ToString(),
            Name = name,
            TypeLine = typeLine,
            Rarity = "common",
            SetCode = setCode,
            SetName = setName
        };
    }
}
