using FluentAssertions;
using FluentValidation.TestHelper;
using MtgDecker.Application.Decks;
using MtgDecker.Application.Interfaces;
using MtgDecker.Domain.Entities;
using MtgDecker.Domain.Enums;
using NSubstitute;

namespace MtgDecker.Application.Tests.Decks;

public class CloneDeckCommandTests
{
    private readonly IDeckRepository _deckRepository = Substitute.For<IDeckRepository>();
    private readonly TimeProvider _timeProvider = TimeProvider.System;

    [Fact]
    public async Task Handle_ClonesSystemDeckToUser()
    {
        // Arrange
        var sourceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var cardId = Guid.NewGuid();

        var sourceDeck = new Deck
        {
            Id = sourceId,
            Name = "PM Goblins",
            Format = Format.Premodern,
            Description = "Goblin tribal",
            UserId = null,
            Entries = new List<DeckEntry>
            {
                new() { CardId = cardId, Quantity = 4, Category = DeckCategory.MainDeck }
            }
        };

        _deckRepository.GetByIdAsync(sourceId, Arg.Any<CancellationToken>())
            .Returns(sourceDeck);

        var handler = new CloneDeckCommandHandler(_deckRepository, _timeProvider);
        var command = new CloneDeckCommand(sourceId, userId);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Name.Should().Be("PM Goblins");
        result.Format.Should().Be(Format.Premodern);
        result.Description.Should().Be("Goblin tribal");
        result.UserId.Should().Be(userId);
        result.Entries.Should().HaveCount(1);
        result.Entries[0].CardId.Should().Be(cardId);
        result.Entries[0].Quantity.Should().Be(4);
        result.Entries[0].Category.Should().Be(DeckCategory.MainDeck);

        await _deckRepository.Received(1).AddAsync(Arg.Any<Deck>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SourceNotFound_Throws()
    {
        // Arrange
        _deckRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Deck?)null);

        var handler = new CloneDeckCommandHandler(_deckRepository, _timeProvider);
        var command = new CloneDeckCommand(Guid.NewGuid(), Guid.NewGuid());

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public void Validator_EmptySourceDeckId_Fails()
    {
        var validator = new CloneDeckCommandValidator();
        var command = new CloneDeckCommand(Guid.Empty, Guid.NewGuid());
        var result = validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.SourceDeckId);
    }

    [Fact]
    public void Validator_EmptyUserId_Fails()
    {
        var validator = new CloneDeckCommandValidator();
        var command = new CloneDeckCommand(Guid.NewGuid(), Guid.Empty);
        var result = validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.UserId);
    }
}
