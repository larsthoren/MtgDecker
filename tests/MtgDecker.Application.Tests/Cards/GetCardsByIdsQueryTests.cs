using FluentAssertions;
using NSubstitute;
using MtgDecker.Application.Cards;
using MtgDecker.Application.Interfaces;
using MtgDecker.Domain.Entities;

namespace MtgDecker.Application.Tests.Cards;

public class GetCardsByIdsQueryTests
{
    private readonly ICardRepository _cardRepo = Substitute.For<ICardRepository>();
    private readonly GetCardsByIdsHandler _handler;

    public GetCardsByIdsQueryTests()
    {
        _handler = new GetCardsByIdsHandler(_cardRepo);
    }

    [Fact]
    public async Task Handle_CardsExist_ReturnsMatchingCards()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var cardIds = new List<Guid> { id1, id2 };
        var cards = new List<Card>
        {
            new() { Id = id1, Name = "Lightning Bolt", TypeLine = "Instant" },
            new() { Id = id2, Name = "Counterspell", TypeLine = "Instant" }
        };
        _cardRepo.GetByIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(cards);

        var result = await _handler.Handle(
            new GetCardsByIdsQuery(cardIds), CancellationToken.None);

        result.Should().HaveCount(2);
        result.Select(c => c.Name).Should().Contain("Lightning Bolt");
        result.Select(c => c.Name).Should().Contain("Counterspell");
    }

    [Fact]
    public async Task Handle_NoMatchingCards_ReturnsEmptyList()
    {
        _cardRepo.GetByIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card>());

        var result = await _handler.Handle(
            new GetCardsByIdsQuery(new List<Guid> { Guid.NewGuid() }), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_EmptyIdList_ReturnsEmptyList()
    {
        _cardRepo.GetByIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card>());

        var result = await _handler.Handle(
            new GetCardsByIdsQuery(new List<Guid>()), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_PassesCorrectIdsToRepository()
    {
        var cardIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        _cardRepo.GetByIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card>());

        await _handler.Handle(new GetCardsByIdsQuery(cardIds), CancellationToken.None);

        await _cardRepo.Received(1).GetByIdsAsync(cardIds, Arg.Any<CancellationToken>());
    }
}
