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
    private readonly ICardRepository _cardRepo = Substitute.For<ICardRepository>();
    private readonly UpdateCardQuantityHandler _handler;

    public UpdateCardQuantityCommandTests()
    {
        _handler = new UpdateCardQuantityHandler(_deckRepo, _cardRepo, TimeProvider.System);
    }

    [Fact]
    public async Task Handle_UpdatesQuantityAndSaves()
    {
        var card = new Card { Id = Guid.NewGuid(), Name = "Lightning Bolt", TypeLine = "Instant" };
        var deck = new Deck { Id = Guid.NewGuid(), Name = "Test", Format = Format.Modern, UserId = Guid.NewGuid() };
        deck.AddCard(card, 2, DeckCategory.MainDeck, DateTime.UtcNow);
        _deckRepo.GetByIdAsync(deck.Id, Arg.Any<CancellationToken>()).Returns(deck);
        _cardRepo.GetByIdAsync(card.Id, Arg.Any<CancellationToken>()).Returns(card);

        var result = await _handler.Handle(
            new UpdateCardQuantityCommand(deck.Id, card.Id, DeckCategory.MainDeck, 4),
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

    [Fact]
    public async Task Handle_CardNotFound_Throws()
    {
        var deck = new Deck { Id = Guid.NewGuid(), Name = "Test", Format = Format.Modern, UserId = Guid.NewGuid() };
        _deckRepo.GetByIdAsync(deck.Id, Arg.Any<CancellationToken>()).Returns(deck);
        _cardRepo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Card?)null);

        var act = () => _handler.Handle(
            new UpdateCardQuantityCommand(deck.Id, Guid.NewGuid(), DeckCategory.MainDeck, 1),
            CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Handle_SystemDeck_ThrowsInvalidOperationException()
    {
        var deckId = Guid.NewGuid();
        var systemDeck = new Deck { Id = deckId, Name = "System", UserId = null };
        _deckRepo.GetByIdAsync(deckId, Arg.Any<CancellationToken>())
            .Returns(systemDeck);

        var act = () => _handler.Handle(
            new UpdateCardQuantityCommand(deckId, Guid.NewGuid(), DeckCategory.MainDeck, 1),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("System decks cannot be modified.");
    }
}
