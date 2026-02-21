using FluentAssertions;
using NSubstitute;
using MtgDecker.Application.Decks;
using MtgDecker.Application.Interfaces;
using MtgDecker.Domain.Entities;
using MtgDecker.Domain.Enums;

namespace MtgDecker.Application.Tests.Decks;

public class MoveCardCategoryCommandTests
{
    private readonly IDeckRepository _deckRepo = Substitute.For<IDeckRepository>();
    private readonly ICardRepository _cardRepo = Substitute.For<ICardRepository>();
    private readonly MoveCardCategoryHandler _handler;

    public MoveCardCategoryCommandTests()
    {
        _handler = new MoveCardCategoryHandler(_deckRepo, _cardRepo, TimeProvider.System);
    }

    [Fact]
    public async Task Handle_MovesCardAndSaves()
    {
        var cardId = Guid.NewGuid();
        var card = new Card { Id = cardId, Name = "Lightning Bolt", TypeLine = "Instant" };
        var deck = new Deck { Id = Guid.NewGuid(), Name = "Test", Format = Format.Modern, UserId = Guid.NewGuid() };
        deck.AddCard(card, 2, DeckCategory.Maybeboard, DateTime.UtcNow);
        _deckRepo.GetByIdAsync(deck.Id, Arg.Any<CancellationToken>()).Returns(deck);
        _cardRepo.GetByIdAsync(cardId, Arg.Any<CancellationToken>()).Returns(card);

        var result = await _handler.Handle(
            new MoveCardCategoryCommand(deck.Id, cardId, DeckCategory.Maybeboard, DeckCategory.MainDeck),
            CancellationToken.None);

        result.Entries.Should().HaveCount(1);
        result.Entries[0].Category.Should().Be(DeckCategory.MainDeck);
        await _deckRepo.Received(1).UpdateAsync(deck, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DeckNotFound_Throws()
    {
        _deckRepo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Deck?)null);

        var act = () => _handler.Handle(
            new MoveCardCategoryCommand(Guid.NewGuid(), Guid.NewGuid(), DeckCategory.Maybeboard, DeckCategory.MainDeck),
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
            new MoveCardCategoryCommand(deck.Id, Guid.NewGuid(), DeckCategory.Maybeboard, DeckCategory.MainDeck),
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
            new MoveCardCategoryCommand(deckId, Guid.NewGuid(), DeckCategory.Maybeboard, DeckCategory.MainDeck),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("System decks cannot be modified.");
    }
}
