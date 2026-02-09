using FluentAssertions;
using NSubstitute;
using MtgDecker.Application.Decks;
using MtgDecker.Application.Interfaces;
using MtgDecker.Domain.Entities;
using MtgDecker.Domain.Enums;

namespace MtgDecker.Application.Tests.Decks;

public class ListDecksQueryTests
{
    private readonly IDeckRepository _deckRepo = Substitute.For<IDeckRepository>();
    private readonly ListDecksHandler _handler;

    public ListDecksQueryTests()
    {
        _handler = new ListDecksHandler(_deckRepo);
    }

    [Fact]
    public async Task Handle_UserHasDecks_ReturnsDeckList()
    {
        var userId = Guid.NewGuid();
        var decks = new List<Deck>
        {
            new() { Id = Guid.NewGuid(), Name = "Burn", Format = Format.Modern, UserId = userId },
            new() { Id = Guid.NewGuid(), Name = "Control", Format = Format.Legacy, UserId = userId }
        };
        _deckRepo.ListByUserAsync(userId, Arg.Any<CancellationToken>()).Returns(decks);

        var result = await _handler.Handle(new ListDecksQuery(userId), CancellationToken.None);

        result.Should().HaveCount(2);
        result.Should().BeEquivalentTo(decks);
    }

    [Fact]
    public async Task Handle_UserHasNoDecks_ReturnsEmptyList()
    {
        var userId = Guid.NewGuid();
        _deckRepo.ListByUserAsync(userId, Arg.Any<CancellationToken>()).Returns(new List<Deck>());

        var result = await _handler.Handle(new ListDecksQuery(userId), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_PassesCorrectUserIdToRepository()
    {
        var userId = Guid.NewGuid();
        _deckRepo.ListByUserAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(new List<Deck>());

        await _handler.Handle(new ListDecksQuery(userId), CancellationToken.None);

        await _deckRepo.Received(1).ListByUserAsync(userId, Arg.Any<CancellationToken>());
    }
}
