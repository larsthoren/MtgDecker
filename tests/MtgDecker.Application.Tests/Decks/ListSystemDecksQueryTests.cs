using FluentAssertions;
using MtgDecker.Application.Decks;
using MtgDecker.Application.Interfaces;
using MtgDecker.Domain.Entities;
using MtgDecker.Domain.Enums;
using NSubstitute;

namespace MtgDecker.Application.Tests.Decks;

public class ListSystemDecksQueryTests
{
    private readonly IDeckRepository _deckRepo = Substitute.For<IDeckRepository>();
    private readonly ListSystemDecksQueryHandler _handler;

    public ListSystemDecksQueryTests()
    {
        _handler = new ListSystemDecksQueryHandler(_deckRepo);
    }

    [Fact]
    public async Task Handle_ReturnsSystemDecks()
    {
        // Arrange
        var systemDecks = new List<Deck>
        {
            new() { Name = "Legacy Goblins", Format = Format.Legacy, UserId = null },
            new() { Name = "Modern Burn", Format = Format.Modern, UserId = null }
        };
        _deckRepo.ListSystemDecksAsync(Arg.Any<CancellationToken>())
            .Returns(systemDecks);

        var query = new ListSystemDecksQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(d => d.UserId.Should().BeNull());
    }

    [Fact]
    public async Task Handle_NoSystemDecks_ReturnsEmptyList()
    {
        // Arrange
        _deckRepo.ListSystemDecksAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Deck>());

        var query = new ListSystemDecksQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }
}
