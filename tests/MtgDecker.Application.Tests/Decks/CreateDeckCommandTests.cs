using FluentAssertions;
using NSubstitute;
using MtgDecker.Application.Decks;
using MtgDecker.Application.Interfaces;
using MtgDecker.Domain.Entities;
using MtgDecker.Domain.Enums;

namespace MtgDecker.Application.Tests.Decks;

public class CreateDeckCommandTests
{
    private readonly IDeckRepository _deckRepo = Substitute.For<IDeckRepository>();
    private readonly CreateDeckHandler _handler;

    public CreateDeckCommandTests()
    {
        _handler = new CreateDeckHandler(_deckRepo);
    }

    [Fact]
    public async Task Handle_CreatesDeckAndReturnsIt()
    {
        var userId = Guid.NewGuid();
        var command = new CreateDeckCommand("Modern Burn", Format.Modern, "Burn deck", userId);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Name.Should().Be("Modern Burn");
        result.Format.Should().Be(Format.Modern);
        result.Description.Should().Be("Burn deck");
        result.UserId.Should().Be(userId);
        result.Id.Should().NotBeEmpty();
        await _deckRepo.Received(1).AddAsync(Arg.Any<Deck>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Validator_EmptyName_Fails()
    {
        var validator = new CreateDeckValidator();
        var command = new CreateDeckCommand("", Format.Modern, null, Guid.NewGuid());

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Fact]
    public void Validator_EmptyUserId_Fails()
    {
        var validator = new CreateDeckValidator();
        var command = new CreateDeckCommand("Test", Format.Modern, null, Guid.Empty);

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "UserId");
    }
}
