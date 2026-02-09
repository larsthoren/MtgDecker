using FluentAssertions;
using NSubstitute;
using MtgDecker.Application.Collection;
using MtgDecker.Application.Interfaces;
using MtgDecker.Domain.Entities;
using MtgDecker.Domain.Enums;

namespace MtgDecker.Application.Tests.Collection;

public class RemoveFromCollectionCommandTests
{
    private readonly ICollectionRepository _collectionRepo = Substitute.For<ICollectionRepository>();
    private readonly RemoveFromCollectionHandler _handler;

    public RemoveFromCollectionCommandTests()
    {
        _handler = new RemoveFromCollectionHandler(_collectionRepo);
    }

    [Fact]
    public async Task Handle_RemovesEntry()
    {
        var entryId = Guid.NewGuid();
        var entry = new CollectionEntry
        {
            Id = entryId,
            UserId = Guid.NewGuid(),
            CardId = Guid.NewGuid(),
            Quantity = 1,
            IsFoil = false,
            Condition = CardCondition.NearMint
        };
        _collectionRepo.GetByIdAsync(entryId, Arg.Any<CancellationToken>()).Returns(entry);

        await _handler.Handle(new RemoveFromCollectionCommand(entryId), CancellationToken.None);

        await _collectionRepo.Received(1).DeleteAsync(entryId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_EntryNotFound_Throws()
    {
        _collectionRepo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((CollectionEntry?)null);

        var act = () => _handler.Handle(
            new RemoveFromCollectionCommand(Guid.NewGuid()),
            CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
