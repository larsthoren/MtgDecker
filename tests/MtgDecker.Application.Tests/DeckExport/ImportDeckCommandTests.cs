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
        _handler = new ImportDeckHandler(new[] { _mtgoParser }, _cardRepo, _deckRepo);
    }

    [Fact]
    public async Task Handle_ImportsDeckFromText()
    {
        var card = new Card { Id = Guid.NewGuid(), Name = "Lightning Bolt", TypeLine = "Instant" };
        _mtgoParser.Parse(Arg.Any<string>()).Returns(new ParsedDeck
        {
            MainDeck = new List<ParsedDeckEntry> { new() { Quantity = 4, CardName = "Lightning Bolt" } }
        });
        _cardRepo.GetByNameAsync("Lightning Bolt", Arg.Any<CancellationToken>()).Returns(card);

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
        _cardRepo.GetByNameAsync("Fake Card", Arg.Any<CancellationToken>()).Returns((Card?)null);

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
}
