using FluentAssertions;
using NSubstitute;
using MtgDecker.Application.Decks;
using MtgDecker.Application.Interfaces;
using MtgDecker.Domain.Entities;
using MtgDecker.Domain.Enums;

namespace MtgDecker.Application.Tests.Decks;

public class UpdateCardQuantityCommandTests
{
    private readonly IDeckRepository _deckRepo = Substitute.For<IDeckRepository>();
    private readonly UpdateCardQuantityHandler _handler;

    public UpdateCardQuantityCommandTests()
    {
        _handler = new UpdateCardQuantityHandler(_deckRepo, TimeProvider.System);
    }

    [Fact]
    public async Task Handle_UpdatesQuantityAndSaves()
    {
        var cardId = Guid.NewGuid();
        var deck = new Deck { Id = Guid.NewGuid(), Name = "Test", Format = Format.Modern, UserId = Guid.NewGuid() };
        deck.AddCard(new Card { Id = cardId, Name = "Lightning Bolt", TypeLine = "Instant" }, 2, DeckCategory.MainDeck);
        _deckRepo.GetByIdAsync(deck.Id, Arg.Any<CancellationToken>()).Returns(deck);

        var result = await _handler.Handle(
            new UpdateCardQuantityCommand(deck.Id, cardId, DeckCategory.MainDeck, 4),
            CancellationToken.None);

        result.Entries[0].Quantity.Should().Be(4);
        await _deckRepo.Received(1).UpdateAsync(deck, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DeckNotFound_Throws()
    {
        _deckRepo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Deck?)null);

        var act = () => _handler.Handle(
            new UpdateCardQuantityCommand(Guid.NewGuid(), Guid.NewGuid(), DeckCategory.MainDeck, 1),
            CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
