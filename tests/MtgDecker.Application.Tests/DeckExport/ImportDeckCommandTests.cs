using FluentAssertions;
using NSubstitute;
using MtgDecker.Application.DeckExport;
using MtgDecker.Application.Interfaces;
using MtgDecker.Domain.Entities;
using MtgDecker.Domain.Enums;

namespace MtgDecker.Application.Tests.DeckExport;

public class ImportDeckCommandTests
{
    private readonly ICardRepository _cardRepo = Substitute.For<ICardRepository>();
    private readonly IDeckRepository _deckRepo = Substitute.For<IDeckRepository>();
    private readonly IDeckParser _mtgoParser = Substitute.For<IDeckParser>();
    private readonly ImportDeckHandler _handler;

    public ImportDeckCommandTests()
    {
        _mtgoParser.FormatName.Returns("MTGO");
        _handler = new ImportDeckHandler(new[] { _mtgoParser }, _cardRepo, _deckRepo, TimeProvider.System);
    }

    [Fact]
    public async Task Handle_ImportsDeckFromText()
    {
        var card = new Card { Id = Guid.NewGuid(), Name = "Lightning Bolt", TypeLine = "Instant" };
        _mtgoParser.Parse(Arg.Any<string>()).Returns(new ParsedDeck
        {
            MainDeck = new List<ParsedDeckEntry> { new() { Quantity = 4, CardName = "Lightning Bolt" } }
        });
        _cardRepo.GetByNamesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card> { card });

        var result = await _handler.Handle(
            new ImportDeckCommand("4 Lightning Bolt", "MTGO", "Burn", Format.Modern, Guid.NewGuid()),
            CancellationToken.None);

        result.Deck.Entries.Should().HaveCount(1);
        result.UnresolvedCards.Should().BeEmpty();
        await _deckRepo.Received(1).AddAsync(Arg.Any<Deck>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnresolvedCard_ListedInResult()
    {
        _mtgoParser.Parse(Arg.Any<string>()).Returns(new ParsedDeck
        {
            MainDeck = new List<ParsedDeckEntry> { new() { Quantity = 4, CardName = "Fake Card" } }
        });
        _cardRepo.GetByNamesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card>());

        var result = await _handler.Handle(
            new ImportDeckCommand("4 Fake Card", "MTGO", "Test", Format.Modern, Guid.NewGuid()),
            CancellationToken.None);

        result.UnresolvedCards.Should().Contain("Fake Card");
        result.Deck.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_UnknownParser_Throws()
    {
        var act = () => _handler.Handle(
            new ImportDeckCommand("4 Bolt", "UnknownFormat", "Test", Format.Modern, Guid.NewGuid()),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Handle_BatchFetchesCards_NotOneByOne()
    {
        var bolt = new Card { Id = Guid.NewGuid(), Name = "Lightning Bolt", TypeLine = "Instant" };
        var guide = new Card { Id = Guid.NewGuid(), Name = "Goblin Guide", TypeLine = "Creature" };
        _mtgoParser.Parse(Arg.Any<string>()).Returns(new ParsedDeck
        {
            MainDeck = new List<ParsedDeckEntry>
            {
                new() { Quantity = 4, CardName = "Lightning Bolt" },
                new() { Quantity = 4, CardName = "Goblin Guide" }
            },
            Sideboard = new List<ParsedDeckEntry>
            {
                new() { Quantity = 2, CardName = "Lightning Bolt" }
            }
        });
        _cardRepo.GetByNamesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card> { bolt, guide });

        var result = await _handler.Handle(
            new ImportDeckCommand("deck text", "MTGO", "Burn", Format.Modern, Guid.NewGuid()),
            CancellationToken.None);

        // Should call GetByNamesAsync exactly once (batch), never GetByNameAsync
        await _cardRepo.Received(1).GetByNamesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>());
        await _cardRepo.DidNotReceive().GetByNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());

        // Bolt in MainDeck + Guide in MainDeck + Bolt in Sideboard = 3 entries
        result.Deck.Entries.Should().HaveCount(3);
        result.UnresolvedCards.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_MainDeckAndSideboard_BothResolved()
    {
        var bolt = new Card { Id = Guid.NewGuid(), Name = "Lightning Bolt", TypeLine = "Instant" };
        var guide = new Card { Id = Guid.NewGuid(), Name = "Goblin Guide", TypeLine = "Creature" };
        _mtgoParser.Parse(Arg.Any<string>()).Returns(new ParsedDeck
        {
            MainDeck = new List<ParsedDeckEntry>
            {
                new() { Quantity = 4, CardName = "Lightning Bolt" }
            },
            Sideboard = new List<ParsedDeckEntry>
            {
                new() { Quantity = 2, CardName = "Goblin Guide" }
            }
        });
        _cardRepo.GetByNamesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card> { bolt, guide });

        var result = await _handler.Handle(
            new ImportDeckCommand("deck text", "MTGO", "Burn", Format.Modern, Guid.NewGuid()),
            CancellationToken.None);

        result.Deck.Entries.Should().HaveCount(2);
        result.Deck.Entries.Should().Contain(e => e.CardId == bolt.Id && e.Category == DeckCategory.MainDeck);
        result.Deck.Entries.Should().Contain(e => e.CardId == guide.Id && e.Category == DeckCategory.Sideboard);
    }
}
