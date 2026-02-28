using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using MtgDecker.Application.DeckExport;
using MtgDecker.Application.Interfaces;
using MtgDecker.Domain.Entities;

namespace MtgDecker.Application.Tests.DeckExport;

public class SeedPresetCardDataCommandTests
{
    private readonly ICardRepository _cardRepository = Substitute.For<ICardRepository>();
    private readonly IScryfallClient _scryfallClient = Substitute.For<IScryfallClient>();
    private readonly ILogger<SeedPresetCardDataHandler> _logger = Substitute.For<ILogger<SeedPresetCardDataHandler>>();
    private readonly SeedPresetCardDataHandler _handler;

    public SeedPresetCardDataCommandTests()
    {
        _handler = new SeedPresetCardDataHandler(_cardRepository, _scryfallClient, _logger);
    }

    [Fact]
    public async Task SkipsScryfall_WhenAllCardsExist()
    {
        _cardRepository.GetByNamesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var names = callInfo.Arg<IEnumerable<string>>().ToList();
                return names.Select(n => new Card { Name = n }).ToList();
            });

        var result = await _handler.Handle(new SeedPresetCardDataCommand(), CancellationToken.None);

        result.SeededCount.Should().Be(0);
        result.NotFoundOnScryfall.Should().BeEmpty();
        await _scryfallClient.DidNotReceive()
            .FetchCardsByNamesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FetchesMissingCards_FromScryfall()
    {
        _cardRepository.GetByNamesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card>());

        _scryfallClient.FetchCardsByNamesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var names = callInfo.Arg<IEnumerable<string>>().ToList();
                var cards = names.Take(5).Select(n => new Card { Name = n }).ToList();
                var notFound = names.Skip(5).Take(1).ToList();
                return (cards, notFound);
            });

        var result = await _handler.Handle(new SeedPresetCardDataCommand(), CancellationToken.None);

        result.SeededCount.Should().Be(5);
        await _cardRepository.Received(1)
            .UpsertBatchAsync(Arg.Any<IEnumerable<Card>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReportsNotFoundCards()
    {
        _cardRepository.GetByNamesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card>());

        _scryfallClient.FetchCardsByNamesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns((new List<Card>(), new List<string> { "Unknown Card" }));

        var result = await _handler.Handle(new SeedPresetCardDataCommand(), CancellationToken.None);

        result.NotFoundOnScryfall.Should().Contain("Unknown Card");
    }

    [Fact]
    public async Task ParsesCardNames_FromPresetRegistry()
    {
        var capturedNames = new List<string>();
        _cardRepository.GetByNamesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedNames = callInfo.Arg<IEnumerable<string>>().ToList();
                return capturedNames.Select(n => new Card { Name = n }).ToList();
            });

        await _handler.Handle(new SeedPresetCardDataCommand(), CancellationToken.None);

        capturedNames.Should().Contain("Goblin Lackey");
        capturedNames.Should().Contain("Mountain");
        capturedNames.Select(n => n.ToLowerInvariant()).Should().OnlyHaveUniqueItems();
    }
}
