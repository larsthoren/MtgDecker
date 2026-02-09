using FluentAssertions;
using NSubstitute;
using MtgDecker.Application.Decks;
using MtgDecker.Application.Interfaces;
using MtgDecker.Domain.Entities;
using MtgDecker.Domain.Enums;

namespace MtgDecker.Application.Tests.Decks;

public class DeleteDeckCommandTests
{
    private readonly IDeckRepository _deckRepo = Substitute.For<IDeckRepository>();
    private readonly DeleteDeckHandler _handler;

    public DeleteDeckCommandTests()
    {
        _handler = new DeleteDeckHandler(_deckRepo);
    }

    [Fact]
    public async Task Handle_DeletesDeck()
    {
        var deck = new Deck { Id = Guid.NewGuid(), Name = "Test", Format = Format.Modern, UserId = Guid.NewGuid() };
        _deckRepo.GetByIdAsync(deck.Id, Arg.Any<CancellationToken>()).Returns(deck);

        await _handler.Handle(new DeleteDeckCommand(deck.Id), CancellationToken.None);

        await _deckRepo.Received(1).DeleteAsync(deck.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DeckNotFound_Throws()
    {
        _deckRepo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Deck?)null);

        var act = () => _handler.Handle(
            new DeleteDeckCommand(Guid.NewGuid()),
            CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
