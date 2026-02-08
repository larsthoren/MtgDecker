using FluentAssertions;
using NSubstitute;
using MtgDecker.Application.Decks;
using MtgDecker.Application.Interfaces;
using MtgDecker.Domain.Entities;
using MtgDecker.Domain.Enums;

namespace MtgDecker.Application.Tests.Decks;

public class GetDeckShortagesQueryTests
{
    private readonly IDeckRepository _deckRepo = Substitute.For<IDeckRepository>();
    private readonly ICollectionRepository _collectionRepo = Substitute.For<ICollectionRepository>();
    private readonly ICardRepository _cardRepo = Substitute.For<ICardRepository>();
    private readonly GetDeckShortagesHandler _handler;

    public GetDeckShortagesQueryTests()
    {
        _handler = new GetDeckShortagesHandler(_deckRepo, _collectionRepo, _cardRepo);
    }

    [Fact]
    public async Task Handle_ReturnsShortages()
    {
        var userId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var card = new Card { Id = cardId, Name = "Lightning Bolt", OracleId = "oracle1", TypeLine = "Instant" };

        var deck = new Deck { Id = Guid.NewGuid(), Format = Format.Modern, UserId = userId };
        deck.AddCard(card, 4, DeckCategory.MainDeck);

        var collection = new List<CollectionEntry>
        {
            new() { UserId = userId, CardId = cardId, Quantity = 2 }
        };

        _deckRepo.GetByIdAsync(deck.Id, Arg.Any<CancellationToken>()).Returns(deck);
        _collectionRepo.GetByUserAsync(userId, Arg.Any<CancellationToken>()).Returns(collection);
        _cardRepo.GetByIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card> { card });

        var result = await _handler.Handle(
            new GetDeckShortagesQuery(deck.Id, userId),
            CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].CardName.Should().Be("Lightning Bolt");
        result[0].Needed.Should().Be(4);
        result[0].Owned.Should().Be(2);
        result[0].Shortage.Should().Be(2);
    }

    [Fact]
    public async Task Handle_NoShortage_ReturnsEmpty()
    {
        var userId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var card = new Card { Id = cardId, Name = "Lightning Bolt", OracleId = "oracle1", TypeLine = "Instant" };

        var deck = new Deck { Id = Guid.NewGuid(), Format = Format.Modern, UserId = userId };
        deck.AddCard(card, 4, DeckCategory.MainDeck);

        var collection = new List<CollectionEntry>
        {
            new() { UserId = userId, CardId = cardId, Quantity = 4 }
        };

        _deckRepo.GetByIdAsync(deck.Id, Arg.Any<CancellationToken>()).Returns(deck);
        _collectionRepo.GetByUserAsync(userId, Arg.Any<CancellationToken>()).Returns(collection);
        _cardRepo.GetByIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card> { card });

        var result = await _handler.Handle(
            new GetDeckShortagesQuery(deck.Id, userId),
            CancellationToken.None);

        result.Should().BeEmpty();
    }
}
