using FluentAssertions;
using NSubstitute;
using MtgDecker.Application.Cards;
using MtgDecker.Application.Interfaces;
using MtgDecker.Domain.Entities;

namespace MtgDecker.Application.Tests.Cards;

public class SearchCardsQueryTests
{
    private readonly ICardRepository _cardRepo = Substitute.For<ICardRepository>();
    private readonly SearchCardsHandler _handler;

    public SearchCardsQueryTests()
    {
        _handler = new SearchCardsHandler(_cardRepo);
    }

    [Fact]
    public async Task Handle_ReturnsSearchResults()
    {
        var filter = new CardSearchFilter { SearchText = "Bolt" };
        var cards = new List<Card> { new() { Name = "Lightning Bolt" } };
        _cardRepo.SearchAsync(filter, Arg.Any<CancellationToken>())
            .Returns((cards, 1));

        var result = await _handler.Handle(new SearchCardsQuery(filter), CancellationToken.None);

        result.Cards.Should().HaveCount(1);
        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_EmptySearch_ReturnsEmpty()
    {
        var filter = new CardSearchFilter { SearchText = "NonExistent" };
        _cardRepo.SearchAsync(filter, Arg.Any<CancellationToken>())
            .Returns((new List<Card>(), 0));

        var result = await _handler.Handle(new SearchCardsQuery(filter), CancellationToken.None);

        result.Cards.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }
}
