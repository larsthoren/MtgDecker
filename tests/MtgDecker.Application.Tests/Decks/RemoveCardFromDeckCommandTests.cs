using FluentAssertions;
using NSubstitute;
using MtgDecker.Application.Decks;
using MtgDecker.Application.Interfaces;
using MtgDecker.Domain.Entities;
using MtgDecker.Domain.Enums;

namespace MtgDecker.Application.Tests.Decks;

public class RemoveCardFromDeckCommandTests
{
    private readonly IDeckRepository _deckRepo = Substitute.For<IDeckRepository>();
    private readonly RemoveCardFromDeckHandler _handler;

    public RemoveCardFromDeckCommandTests()
    {
        _handler = new RemoveCardFromDeckHandler(_deckRepo, TimeProvider.System);
    }

    [Fact]
    public async Task Handle_RemovesCardAndSaves()
    {
        var cardId = Guid.NewGuid();
        var deck = new Deck { Id = Guid.NewGuid(), Name = "Test", Format = Format.Modern, UserId = Guid.NewGuid() };
        deck.AddCard(new Card { Id = cardId, Name = "Lightning Bolt", TypeLine = "Instant" }, 4, DeckCategory.MainDeck);
        _deckRepo.GetByIdAsync(deck.Id, Arg.Any<CancellationToken>()).Returns(deck);

        var result = await _handler.Handle(
            new RemoveCardFromDeckCommand(deck.Id, cardId, DeckCategory.MainDeck),
            CancellationToken.None);

        result.Entries.Should().BeEmpty();
        await _deckRepo.Received(1).UpdateAsync(deck, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DeckNotFound_Throws()
    {
        _deckRepo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Deck?)null);

        var act = () => _handler.Handle(
            new RemoveCardFromDeckCommand(Guid.NewGuid(), Guid.NewGuid(), DeckCategory.MainDeck),
            CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
