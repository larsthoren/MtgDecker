using FluentAssertions;
using NSubstitute;
using MtgDecker.Application.Collection;
using MtgDecker.Application.Interfaces;
using MtgDecker.Domain.Entities;
using MtgDecker.Domain.Enums;

namespace MtgDecker.Application.Tests.Collection;

public class SearchCollectionQueryTests
{
    private readonly ICollectionRepository _collectionRepo = Substitute.For<ICollectionRepository>();
    private readonly SearchCollectionHandler _handler;

    public SearchCollectionQueryTests()
    {
        _handler = new SearchCollectionHandler(_collectionRepo);
    }

    [Fact]
    public async Task Handle_WithSearchText_ReturnsMatchingEntries()
    {
        var userId = Guid.NewGuid();
        var entries = new List<CollectionEntry>
        {
            new()
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CardId = Guid.NewGuid(),
                Quantity = 4,
                IsFoil = false,
                Condition = CardCondition.NearMint
            }
        };
        _collectionRepo.SearchAsync(userId, "Bolt", Arg.Any<CancellationToken>()).Returns(entries);

        var result = await _handler.Handle(
            new SearchCollectionQuery(userId, "Bolt"), CancellationToken.None);

        result.Should().HaveCount(1);
        result.Should().BeEquivalentTo(entries);
    }

    [Fact]
    public async Task Handle_WithNullSearchText_ReturnsAllEntries()
    {
        var userId = Guid.NewGuid();
        var entries = new List<CollectionEntry>
        {
            new()
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CardId = Guid.NewGuid(),
                Quantity = 2,
                IsFoil = true,
                Condition = CardCondition.LightlyPlayed
            },
            new()
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CardId = Guid.NewGuid(),
                Quantity = 1,
                IsFoil = false,
                Condition = CardCondition.NearMint
            }
        };
        _collectionRepo.SearchAsync(userId, null, Arg.Any<CancellationToken>()).Returns(entries);

        var result = await _handler.Handle(
            new SearchCollectionQuery(userId), CancellationToken.None);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_NoResults_ReturnsEmptyList()
    {
        var userId = Guid.NewGuid();
        _collectionRepo.SearchAsync(userId, "Nonexistent", Arg.Any<CancellationToken>())
            .Returns(new List<CollectionEntry>());

        var result = await _handler.Handle(
            new SearchCollectionQuery(userId, "Nonexistent"), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_PassesCorrectParametersToRepository()
    {
        var userId = Guid.NewGuid();
        _collectionRepo.SearchAsync(Arg.Any<Guid>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new List<CollectionEntry>());

        await _handler.Handle(
            new SearchCollectionQuery(userId, "Lightning"), CancellationToken.None);

        await _collectionRepo.Received(1).SearchAsync(userId, "Lightning", Arg.Any<CancellationToken>());
    }
}
