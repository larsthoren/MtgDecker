using FluentAssertions;
using NSubstitute;
using MtgDecker.Application.Collection;
using MtgDecker.Application.Interfaces;
using MtgDecker.Domain.Entities;
using MtgDecker.Domain.Enums;

namespace MtgDecker.Application.Tests.Collection;

public class AddToCollectionCommandTests
{
    private readonly ICollectionRepository _collectionRepo = Substitute.For<ICollectionRepository>();
    private readonly ICardRepository _cardRepo = Substitute.For<ICardRepository>();
    private readonly AddToCollectionHandler _handler;

    public AddToCollectionCommandTests()
    {
        _handler = new AddToCollectionHandler(_collectionRepo, _cardRepo);
    }

    [Fact]
    public async Task Handle_AddsEntry()
    {
        var userId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var card = new Card { Id = cardId, Name = "Lightning Bolt", TypeLine = "Instant" };
        _cardRepo.GetByIdAsync(cardId, Arg.Any<CancellationToken>()).Returns(card);

        var result = await _handler.Handle(
            new AddToCollectionCommand(userId, cardId, 2, true, CardCondition.NearMint),
            CancellationToken.None);

        result.UserId.Should().Be(userId);
        result.CardId.Should().Be(cardId);
        result.Quantity.Should().Be(2);
        result.IsFoil.Should().BeTrue();
        result.Condition.Should().Be(CardCondition.NearMint);
        await _collectionRepo.Received(1).AddAsync(Arg.Any<CollectionEntry>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CardNotFound_Throws()
    {
        _cardRepo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Card?)null);

        var act = () => _handler.Handle(
            new AddToCollectionCommand(Guid.NewGuid(), Guid.NewGuid(), 1, false, CardCondition.NearMint),
            CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public void Validator_ZeroQuantity_Fails()
    {
        var validator = new AddToCollectionValidator();
        var command = new AddToCollectionCommand(Guid.NewGuid(), Guid.NewGuid(), 0, false, CardCondition.NearMint);

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
    }
}
