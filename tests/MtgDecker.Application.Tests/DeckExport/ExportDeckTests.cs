using FluentAssertions;
using NSubstitute;
using MtgDecker.Application.DeckExport;
using MtgDecker.Application.Interfaces;
using MtgDecker.Domain.Entities;
using MtgDecker.Domain.Enums;

namespace MtgDecker.Application.Tests.DeckExport;

public class ExportDeckTests
{
    private readonly IDeckRepository _deckRepo = Substitute.For<IDeckRepository>();
    private readonly ICardRepository _cardRepo = Substitute.For<ICardRepository>();

    [Fact]
    public async Task Handle_TextFormat_ReturnsSimpleList()
    {
        var (deck, cards) = CreateTestDeckWithCards();
        SetupMocks(deck, cards);
        var handler = new ExportDeckHandler(_deckRepo, _cardRepo);

        var result = await handler.Handle(new ExportDeckQuery(deck.Id, "Text"), CancellationToken.None);

        result.Should().Contain("4 Lightning Bolt");
        result.Should().Contain("2 Counterspell");
        result.Should().NotContain("(");
    }

    [Fact]
    public async Task Handle_TextFormat_IncludesSideboard()
    {
        var (deck, cards) = CreateTestDeckWithSideboard();
        SetupMocks(deck, cards);
        var handler = new ExportDeckHandler(_deckRepo, _cardRepo);

        var result = await handler.Handle(new ExportDeckQuery(deck.Id, "Text"), CancellationToken.None);

        result.Should().Contain("Sideboard");
        result.Should().Contain("2 Pyroblast");
    }

    [Fact]
    public async Task Handle_CsvFormat_ReturnsHeaderAndRows()
    {
        var (deck, cards) = CreateTestDeckWithCards();
        SetupMocks(deck, cards);
        var handler = new ExportDeckHandler(_deckRepo, _cardRepo);

        var result = await handler.Handle(new ExportDeckQuery(deck.Id, "CSV"), CancellationToken.None);

        var lines = result.Split(Environment.NewLine);
        lines[0].Should().Be("Quantity,Name,Set,Category");
        lines.Should().Contain(l => l.Contains("4,Lightning Bolt,LEA,MainDeck"));
        lines.Should().Contain(l => l.Contains("2,Counterspell,LEA,MainDeck"));
    }

    [Fact]
    public async Task Handle_CsvFormat_EscapesCommasInNames()
    {
        var cardId = Guid.NewGuid();
        var card = new Card
        {
            Id = cardId, Name = "Jace, the Mind Sculptor", TypeLine = "Planeswalker",
            Rarity = "mythic", SetCode = "wwk", SetName = "Worldwake",
            ScryfallId = "a", OracleId = "a", CollectorNumber = "31"
        };
        var deck = new Deck
        {
            Id = Guid.NewGuid(), Name = "Test", Format = Format.Legacy, UserId = Guid.NewGuid(),
            Entries = new List<DeckEntry>
            {
                new() { Id = Guid.NewGuid(), CardId = cardId, Quantity = 3, Category = DeckCategory.MainDeck }
            }
        };
        SetupMocks(deck, new List<Card> { card });
        var handler = new ExportDeckHandler(_deckRepo, _cardRepo);

        var result = await handler.Handle(new ExportDeckQuery(deck.Id, "CSV"), CancellationToken.None);

        result.Should().Contain("3,\"Jace, the Mind Sculptor\",WWK,MainDeck");
    }

    [Fact]
    public async Task Handle_ArenaFormat_StillWorks()
    {
        var (deck, cards) = CreateTestDeckWithCards();
        SetupMocks(deck, cards);
        var handler = new ExportDeckHandler(_deckRepo, _cardRepo);

        var result = await handler.Handle(new ExportDeckQuery(deck.Id, "Arena"), CancellationToken.None);

        result.Should().StartWith("Deck");
        result.Should().Contain("4 Lightning Bolt (LEA)");
    }

    private (Deck, List<Card>) CreateTestDeckWithCards()
    {
        var bolt = new Card { Id = Guid.NewGuid(), Name = "Lightning Bolt", TypeLine = "Instant", Rarity = "common", SetCode = "lea", SetName = "Alpha", ScryfallId = "a", OracleId = "a", CollectorNumber = "161" };
        var counter = new Card { Id = Guid.NewGuid(), Name = "Counterspell", TypeLine = "Instant", Rarity = "uncommon", SetCode = "lea", SetName = "Alpha", ScryfallId = "b", OracleId = "b", CollectorNumber = "54" };
        var deck = new Deck
        {
            Id = Guid.NewGuid(), Name = "Test", Format = Format.Legacy, UserId = Guid.NewGuid(),
            Entries = new List<DeckEntry>
            {
                new() { Id = Guid.NewGuid(), CardId = bolt.Id, Quantity = 4, Category = DeckCategory.MainDeck },
                new() { Id = Guid.NewGuid(), CardId = counter.Id, Quantity = 2, Category = DeckCategory.MainDeck }
            }
        };
        return (deck, new List<Card> { bolt, counter });
    }

    private (Deck, List<Card>) CreateTestDeckWithSideboard()
    {
        var bolt = new Card { Id = Guid.NewGuid(), Name = "Lightning Bolt", TypeLine = "Instant", Rarity = "common", SetCode = "lea", SetName = "Alpha", ScryfallId = "a", OracleId = "a", CollectorNumber = "161" };
        var pyro = new Card { Id = Guid.NewGuid(), Name = "Pyroblast", TypeLine = "Instant", Rarity = "common", SetCode = "ice", SetName = "Ice Age", ScryfallId = "c", OracleId = "c", CollectorNumber = "212" };
        var deck = new Deck
        {
            Id = Guid.NewGuid(), Name = "Test", Format = Format.Legacy, UserId = Guid.NewGuid(),
            Entries = new List<DeckEntry>
            {
                new() { Id = Guid.NewGuid(), CardId = bolt.Id, Quantity = 4, Category = DeckCategory.MainDeck },
                new() { Id = Guid.NewGuid(), CardId = pyro.Id, Quantity = 2, Category = DeckCategory.Sideboard }
            }
        };
        return (deck, new List<Card> { bolt, pyro });
    }

    private void SetupMocks(Deck deck, List<Card> cards)
    {
        _deckRepo.GetByIdAsync(deck.Id, Arg.Any<CancellationToken>()).Returns(deck);
        _cardRepo.GetByIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>()).Returns(cards);
    }
}
