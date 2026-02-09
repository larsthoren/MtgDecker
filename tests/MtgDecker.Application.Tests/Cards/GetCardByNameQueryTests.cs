using FluentAssertions;
using NSubstitute;
using MtgDecker.Application.Cards;
using MtgDecker.Application.Interfaces;
using MtgDecker.Domain.Entities;

namespace MtgDecker.Application.Tests.Cards;

public class GetCardByNameQueryTests
{
    private readonly ICardRepository _cardRepo = Substitute.For<ICardRepository>();
    private readonly GetCardByNameHandler _handler;

    public GetCardByNameQueryTests()
    {
        _handler = new GetCardByNameHandler(_cardRepo);
    }

    [Fact]
    public async Task Handle_CardExists_ReturnsCard()
    {
        var card = new Card
        {
            Id = Guid.NewGuid(),
            Name = "Lightning Bolt",
            TypeLine = "Instant",
            ManaCost = "{R}"
        };
        _cardRepo.GetByNameAsync("Lightning Bolt", Arg.Any<CancellationToken>()).Returns(card);

        var result = await _handler.Handle(
            new GetCardByNameQuery("Lightning Bolt"), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Lightning Bolt");
        result.TypeLine.Should().Be("Instant");
    }

    [Fact]
    public async Task Handle_CardDoesNotExist_ReturnsNull()
    {
        _cardRepo.GetByNameAsync("Nonexistent Card", Arg.Any<CancellationToken>())
            .Returns((Card?)null);

        var result = await _handler.Handle(
            new GetCardByNameQuery("Nonexistent Card"), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_PassesCorrectNameToRepository()
    {
        _cardRepo.GetByNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((Card?)null);

        await _handler.Handle(
            new GetCardByNameQuery("Black Lotus"), CancellationToken.None);

        await _cardRepo.Received(1).GetByNameAsync("Black Lotus", Arg.Any<CancellationToken>());
    }
}
