using FluentAssertions;
using MtgDecker.Infrastructure.Data.Repositories;
using MtgDecker.Infrastructure.Scryfall;
using MtgDecker.Infrastructure.Tests.Data;

namespace MtgDecker.Infrastructure.Tests.Scryfall;

public class BulkDataImporterTests
{
    [Fact]
    public async Task ImportFromStreamAsync_ImportsAllCards()
    {
        using var context = TestDbContextFactory.Create();
        var cardRepo = new CardRepository(context);
        var importer = new BulkDataImporter(cardRepo);

        using var stream = File.OpenRead("Scryfall/Fixtures/sample-cards.json");

        var count = await importer.ImportFromStreamAsync(stream);

        count.Should().Be(3);
        context.Cards.Count().Should().Be(3);
    }

    [Fact]
    public async Task ImportFromStreamAsync_MapsCardFieldsCorrectly()
    {
        using var context = TestDbContextFactory.Create();
        var cardRepo = new CardRepository(context);
        var importer = new BulkDataImporter(cardRepo);

        using var stream = File.OpenRead("Scryfall/Fixtures/sample-cards.json");
        await importer.ImportFromStreamAsync(stream);

        var bolt = context.Cards.FirstOrDefault(c => c.Name == "Lightning Bolt");
        bolt.Should().NotBeNull();
        bolt!.ManaCost.Should().Be("{R}");
        bolt.Cmc.Should().Be(1.0);
        bolt.Rarity.Should().Be("uncommon");
        bolt.SetCode.Should().Be("lea");
        bolt.ImageUri.Should().Contain("bolt.jpg");
    }

    [Fact]
    public async Task ImportFromStreamAsync_MapsMultiFaceCard()
    {
        using var context = TestDbContextFactory.Create();
        var cardRepo = new CardRepository(context);
        var importer = new BulkDataImporter(cardRepo);

        using var stream = File.OpenRead("Scryfall/Fixtures/sample-cards.json");
        await importer.ImportFromStreamAsync(stream);

        var delver = context.Cards.FirstOrDefault(c => c.Name.Contains("Delver"));
        delver.Should().NotBeNull();
        delver!.Layout.Should().Be("transform");

        var faces = context.CardFaces.Where(f => f.CardId == delver.Id).ToList();
        faces.Should().HaveCount(2);
    }

    [Fact]
    public async Task ImportFromStreamAsync_MapsLegalities()
    {
        using var context = TestDbContextFactory.Create();
        var cardRepo = new CardRepository(context);
        var importer = new BulkDataImporter(cardRepo);

        using var stream = File.OpenRead("Scryfall/Fixtures/sample-cards.json");
        await importer.ImportFromStreamAsync(stream);

        var bolt = context.Cards.FirstOrDefault(c => c.Name == "Lightning Bolt");
        bolt.Should().NotBeNull();

        var legalities = context.Set<Domain.ValueObjects.CardLegality>()
            .Where(l => Microsoft.EntityFrameworkCore.EF.Property<Guid>(l, "CardId") == bolt!.Id)
            .ToList();
        legalities.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task ImportFromStreamAsync_ReportsProgress()
    {
        using var context = TestDbContextFactory.Create();
        var cardRepo = new CardRepository(context);
        var importer = new BulkDataImporter(cardRepo);

        var progressValues = new List<int>();
        var progress = new Progress<int>(v => progressValues.Add(v));

        using var stream = File.OpenRead("Scryfall/Fixtures/sample-cards.json");
        await importer.ImportFromStreamAsync(stream, progress);

        // With 3 cards and batch size 1000, we get one report at the end
        progressValues.Should().NotBeEmpty();
    }
}
