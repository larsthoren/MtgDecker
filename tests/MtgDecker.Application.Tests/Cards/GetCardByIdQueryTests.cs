using FluentAssertions;
using NSubstitute;
using MtgDecker.Application.Cards;
using MtgDecker.Application.Interfaces;
using MtgDecker.Domain.Entities;

namespace MtgDecker.Application.Tests.Cards;

public class GetCardByIdQueryTests
{
    private readonly ICardRepository _cardRepo = Substitute.For<ICardRepository>();
    private readonly GetCardByIdHandler _handler;

    public GetCardByIdQueryTests()
    {
        _handler = new GetCardByIdHandler(_cardRepo);
    }

    [Fact]
    public async Task Handle_ExistingCard_ReturnsCard()
    {
        var id = Guid.NewGuid();
        var card = new Card { Id = id, Name = "Lightning Bolt" };
        _cardRepo.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns(card);

        var result = await _handler.Handle(new GetCardByIdQuery(id), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Lightning Bolt");
    }

    [Fact]
    public async Task Handle_NonExistingCard_ReturnsNull()
    {
        _cardRepo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Card?)null);

        var result = await _handler.Handle(new GetCardByIdQuery(Guid.NewGuid()), CancellationToken.None);

        result.Should().BeNull();
    }
}
