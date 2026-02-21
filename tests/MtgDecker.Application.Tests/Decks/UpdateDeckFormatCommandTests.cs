using FluentAssertions;
using NSubstitute;
using MtgDecker.Application.Decks;
using MtgDecker.Application.Interfaces;
using MtgDecker.Domain.Entities;
using MtgDecker.Domain.Enums;

namespace MtgDecker.Application.Tests.Decks;

public class UpdateDeckFormatCommandTests
{
    private readonly IDeckRepository _deckRepo = Substitute.For<IDeckRepository>();
    private readonly UpdateDeckFormatHandler _handler;

    public UpdateDeckFormatCommandTests()
    {
        _handler = new UpdateDeckFormatHandler(_deckRepo, TimeProvider.System);
    }

    [Fact]
    public async Task Handle_UpdatesFormatAndSaves()
    {
        var deck = new Deck
        {
            Id = Guid.NewGuid(), Name = "Goblins", Format = Format.Legacy,
            UserId = Guid.NewGuid(), UpdatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        _deckRepo.GetByIdAsync(deck.Id, Arg.Any<CancellationToken>()).Returns(deck);

        var result = await _handler.Handle(
            new UpdateDeckFormatCommand(deck.Id, Format.Premodern),
            CancellationToken.None);

        result.Format.Should().Be(Format.Premodern);
        result.UpdatedAt.Should().BeAfter(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        await _deckRepo.Received(1).UpdateAsync(deck, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DeckNotFound_Throws()
    {
        _deckRepo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Deck?)null);

        var act = () => _handler.Handle(
            new UpdateDeckFormatCommand(Guid.NewGuid(), Format.Modern),
            CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public void Validator_EmptyDeckId_Fails()
    {
        var validator = new UpdateDeckFormatValidator();
        var result = validator.Validate(new UpdateDeckFormatCommand(Guid.Empty, Format.Legacy));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validator_ValidCommand_Passes()
    {
        var validator = new UpdateDeckFormatValidator();
        var result = validator.Validate(new UpdateDeckFormatCommand(Guid.NewGuid(), Format.Legacy));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_SystemDeck_ThrowsInvalidOperationException()
    {
        var deckId = Guid.NewGuid();
        var systemDeck = new Deck { Id = deckId, Name = "System", UserId = null };
        _deckRepo.GetByIdAsync(deckId, Arg.Any<CancellationToken>())
            .Returns(systemDeck);

        var act = () => _handler.Handle(
            new UpdateDeckFormatCommand(deckId, Format.Modern),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("System decks cannot be modified.");
    }
}
