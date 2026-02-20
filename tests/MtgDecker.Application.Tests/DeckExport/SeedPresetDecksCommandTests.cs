using FluentAssertions;
using MediatR;
using NSubstitute;
using MtgDecker.Application.DeckExport;
using MtgDecker.Application.Decks;
using MtgDecker.Domain.Entities;
using MtgDecker.Domain.Enums;

namespace MtgDecker.Application.Tests.DeckExport;

public class SeedPresetDecksCommandTests
{
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly SeedPresetDecksHandler _handler;
    private readonly Guid _userId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public SeedPresetDecksCommandTests()
    {
        _handler = new SeedPresetDecksHandler(_mediator);
    }

    [Fact]
    public async Task Seeds_NewDeck_WhenNotExisting()
    {
        // No existing decks
        _mediator.Send(Arg.Any<ListDecksQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Deck>());

        // ImportDeckCommand returns success for any deck
        _mediator.Send(Arg.Any<ImportDeckCommand>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var cmd = callInfo.Arg<ImportDeckCommand>();
                return new ImportDeckResult(
                    new Deck { Name = cmd.DeckName, Format = cmd.DeckFormat },
                    new List<string>());
            });

        var result = await _handler.Handle(new SeedPresetDecksCommand(_userId), CancellationToken.None);

        result.Created.Should().HaveCount(PresetDeckRegistry.All.Count);
        result.Skipped.Should().BeEmpty();
        result.Unresolved.Should().BeEmpty();
    }

    [Fact]
    public async Task Skips_ExistingDeck_ByName()
    {
        // First deck already exists
        var firstDeck = PresetDeckRegistry.All[0];
        _mediator.Send(Arg.Any<ListDecksQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Deck> { new() { Name = firstDeck.Name, Format = firstDeck.Format } });

        _mediator.Send(Arg.Any<ImportDeckCommand>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var cmd = callInfo.Arg<ImportDeckCommand>();
                return new ImportDeckResult(
                    new Deck { Name = cmd.DeckName, Format = cmd.DeckFormat },
                    new List<string>());
            });

        var result = await _handler.Handle(new SeedPresetDecksCommand(_userId), CancellationToken.None);

        result.Skipped.Should().Contain(firstDeck.Name);
        result.Created.Should().NotContain(firstDeck.Name);
        result.Created.Should().HaveCount(PresetDeckRegistry.All.Count - 1);
    }

    [Fact]
    public async Task Reports_UnresolvedCards()
    {
        _mediator.Send(Arg.Any<ListDecksQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Deck>());

        // First import has unresolved cards, rest succeed normally
        var firstDeck = PresetDeckRegistry.All[0];
        _mediator.Send(Arg.Any<ImportDeckCommand>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var cmd = callInfo.Arg<ImportDeckCommand>();
                var unresolved = cmd.DeckName == firstDeck.Name
                    ? new List<string> { "Unknown Card" }
                    : new List<string>();
                return new ImportDeckResult(
                    new Deck { Name = cmd.DeckName, Format = cmd.DeckFormat },
                    unresolved);
            });

        var result = await _handler.Handle(new SeedPresetDecksCommand(_userId), CancellationToken.None);

        result.Unresolved.Should().ContainKey(firstDeck.Name);
        result.Unresolved[firstDeck.Name].Should().Contain("Unknown Card");
    }

    [Fact]
    public async Task Seeds_AllRegistryDecks()
    {
        _mediator.Send(Arg.Any<ListDecksQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Deck>());

        _mediator.Send(Arg.Any<ImportDeckCommand>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var cmd = callInfo.Arg<ImportDeckCommand>();
                return new ImportDeckResult(
                    new Deck { Name = cmd.DeckName, Format = cmd.DeckFormat },
                    new List<string>());
            });

        var result = await _handler.Handle(new SeedPresetDecksCommand(_userId), CancellationToken.None);

        // Verify all registry decks were processed
        var allNames = PresetDeckRegistry.All.Select(d => d.Name).ToList();
        result.Created.Should().BeEquivalentTo(allNames);

        // Verify ImportDeckCommand was sent for each deck
        await _mediator.Received(PresetDeckRegistry.All.Count)
            .Send(Arg.Any<ImportDeckCommand>(), Arg.Any<CancellationToken>());
    }
}
