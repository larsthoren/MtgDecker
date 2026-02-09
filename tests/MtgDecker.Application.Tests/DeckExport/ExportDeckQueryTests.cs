using FluentAssertions;
using NSubstitute;
using MtgDecker.Application.DeckExport;
using MtgDecker.Application.Interfaces;
using MtgDecker.Domain.Entities;
using MtgDecker.Domain.Enums;

namespace MtgDecker.Application.Tests.DeckExport;

public class ExportDeckQueryTests
{
    private readonly IDeckRepository _deckRepo = Substitute.For<IDeckRepository>();
    private readonly ICardRepository _cardRepo = Substitute.For<ICardRepository>();
    private readonly ExportDeckHandler _handler;

    public ExportDeckQueryTests()
    {
        _handler = new ExportDeckHandler(_deckRepo, _cardRepo);
    }

    [Fact]
    public async Task Handle_MtgoFormat_OutputsCorrectly()
    {
        var cardId = Guid.NewGuid();
        var sbCardId = Guid.NewGuid();
        var card = new Card { Id = cardId, Name = "Lightning Bolt", SetCode = "lea", CollectorNumber = "161", TypeLine = "Instant" };
        var sbCard = new Card { Id = sbCardId, Name = "Pyroblast", SetCode = "ice", CollectorNumber = "212", TypeLine = "Instant" };

        var deck = new Deck { Id = Guid.NewGuid(), Format = Format.Modern, UserId = Guid.NewGuid() };
        deck.AddCard(card, 4, DeckCategory.MainDeck, DateTime.UtcNow);
        deck.AddCard(sbCard, 2, DeckCategory.Sideboard, DateTime.UtcNow);

        _deckRepo.GetByIdAsync(deck.Id, Arg.Any<CancellationToken>()).Returns(deck);
        _cardRepo.GetByIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card> { card, sbCard });

        var result = await _handler.Handle(new ExportDeckQuery(deck.Id, "MTGO"), CancellationToken.None);

        result.Should().Contain("4 Lightning Bolt");
        result.Should().Contain("SB: 2 Pyroblast");
    }

    [Fact]
    public async Task Handle_ArenaFormat_OutputsCorrectly()
    {
        var cardId = Guid.NewGuid();
        var card = new Card { Id = cardId, Name = "Lightning Bolt", SetCode = "lea", CollectorNumber = "161", TypeLine = "Instant" };

        var deck = new Deck { Id = Guid.NewGuid(), Format = Format.Modern, UserId = Guid.NewGuid() };
        deck.AddCard(card, 4, DeckCategory.MainDeck, DateTime.UtcNow);

        _deckRepo.GetByIdAsync(deck.Id, Arg.Any<CancellationToken>()).Returns(deck);
        _cardRepo.GetByIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card> { card });

        var result = await _handler.Handle(new ExportDeckQuery(deck.Id, "Arena"), CancellationToken.None);

        result.Should().Contain("Deck");
        result.Should().Contain("4 Lightning Bolt (LEA) 161");
    }
}
