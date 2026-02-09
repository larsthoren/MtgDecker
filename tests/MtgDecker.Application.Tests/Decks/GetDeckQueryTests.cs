using FluentAssertions;
using NSubstitute;
using MtgDecker.Application.Decks;
using MtgDecker.Application.Interfaces;
using MtgDecker.Domain.Entities;
using MtgDecker.Domain.Enums;

namespace MtgDecker.Application.Tests.Decks;

public class GetDeckQueryTests
{
    private readonly IDeckRepository _deckRepo = Substitute.For<IDeckRepository>();
    private readonly GetDeckHandler _handler;

    public GetDeckQueryTests()
    {
        _handler = new GetDeckHandler(_deckRepo);
    }

    [Fact]
    public async Task Handle_DeckExists_ReturnsDeck()
    {
        var deckId = Guid.NewGuid();
        var deck = new Deck
        {
            Id = deckId,
            Name = "Modern Burn",
            Format = Format.Modern,
            UserId = Guid.NewGuid()
        };
        _deckRepo.GetByIdAsync(deckId, Arg.Any<CancellationToken>()).Returns(deck);

        var result = await _handler.Handle(new GetDeckQuery(deckId), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(deckId);
        result.Name.Should().Be("Modern Burn");
        await _deckRepo.Received(1).GetByIdAsync(deckId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DeckDoesNotExist_ReturnsNull()
    {
        var deckId = Guid.NewGuid();
        _deckRepo.GetByIdAsync(deckId, Arg.Any<CancellationToken>()).Returns((Deck?)null);

        var result = await _handler.Handle(new GetDeckQuery(deckId), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_PassesCorrectIdToRepository()
    {
        var deckId = Guid.NewGuid();
        _deckRepo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Deck?)null);

        await _handler.Handle(new GetDeckQuery(deckId), CancellationToken.None);

        await _deckRepo.Received(1).GetByIdAsync(deckId, Arg.Any<CancellationToken>());
    }
}
