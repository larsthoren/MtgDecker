using FluentAssertions;
using NSubstitute;
using MtgDecker.Application.Stats;
using MtgDecker.Application.Interfaces;
using MtgDecker.Domain.Entities;
using MtgDecker.Domain.Enums;

namespace MtgDecker.Application.Tests.Stats;

public class GetDeckStatsQueryTests
{
    private readonly IDeckRepository _deckRepo = Substitute.For<IDeckRepository>();
    private readonly ICardRepository _cardRepo = Substitute.For<ICardRepository>();
    private readonly GetDeckStatsHandler _handler;

    public GetDeckStatsQueryTests()
    {
        _handler = new GetDeckStatsHandler(_deckRepo, _cardRepo);
    }

    [Fact]
    public async Task Handle_CalculatesManaCurveAndColors()
    {
        var boltId = Guid.NewGuid();
        var snapId = Guid.NewGuid();
        var bolt = new Card { Id = boltId, Name = "Lightning Bolt", Cmc = 1, Colors = "R", TypeLine = "Instant" };
        var snap = new Card { Id = snapId, Name = "Snapcaster Mage", Cmc = 2, Colors = "U", TypeLine = "Creature — Human Wizard" };

        var deck = new Deck { Id = Guid.NewGuid(), Format = Format.Modern, UserId = Guid.NewGuid() };
        deck.AddCard(bolt, 4, DeckCategory.MainDeck, DateTime.UtcNow);
        deck.AddCard(snap, 3, DeckCategory.MainDeck, DateTime.UtcNow);

        _deckRepo.GetByIdAsync(deck.Id, Arg.Any<CancellationToken>()).Returns(deck);
        _cardRepo.GetByIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card> { bolt, snap });

        var result = await _handler.Handle(new GetDeckStatsQuery(deck.Id), CancellationToken.None);

        result.TotalCards.Should().Be(7);
        result.MainDeckCount.Should().Be(7);
        result.SideboardCount.Should().Be(0);
        result.ManaCurve[1].Should().Be(4); // 4 bolts at CMC 1
        result.ManaCurve[2].Should().Be(3); // 3 snapcasters at CMC 2
        result.ColorDistribution["R"].Should().Be(4);
        result.ColorDistribution["U"].Should().Be(3);
        result.TypeBreakdown["Instant"].Should().Be(4);
        result.TypeBreakdown["Creature"].Should().Be(3);
    }

    [Fact]
    public async Task Handle_LandsExcludedFromManaCurve()
    {
        var landId = Guid.NewGuid();
        var land = new Card { Id = landId, Name = "Mountain", Cmc = 0, Colors = "", TypeLine = "Basic Land — Mountain" };

        var deck = new Deck { Id = Guid.NewGuid(), Format = Format.Modern, UserId = Guid.NewGuid() };
        deck.AddCard(land, 20, DeckCategory.MainDeck, DateTime.UtcNow);

        _deckRepo.GetByIdAsync(deck.Id, Arg.Any<CancellationToken>()).Returns(deck);
        _cardRepo.GetByIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card> { land });

        var result = await _handler.Handle(new GetDeckStatsQuery(deck.Id), CancellationToken.None);

        result.ManaCurve.Should().BeEmpty();
        result.TypeBreakdown.Should().ContainKey("Land");
    }

    [Fact]
    public async Task Handle_CalculatesTotalPrice()
    {
        var cardA = new Card { Id = Guid.NewGuid(), Name = "Bolt", TypeLine = "Instant", PriceUsd = 1.50m, Rarity = "common", SetCode = "lea", SetName = "Alpha", ScryfallId = "a", OracleId = "a" };
        var cardB = new Card { Id = Guid.NewGuid(), Name = "Force", TypeLine = "Instant", PriceUsd = 80.00m, Rarity = "rare", SetCode = "all", SetName = "Alliances", ScryfallId = "b", OracleId = "b" };
        var deck = new Deck
        {
            Id = Guid.NewGuid(), Name = "Test", Format = Format.Legacy, UserId = Guid.NewGuid(),
            Entries = new List<DeckEntry>
            {
                new() { Id = Guid.NewGuid(), CardId = cardA.Id, Quantity = 4, Category = DeckCategory.MainDeck },
                new() { Id = Guid.NewGuid(), CardId = cardB.Id, Quantity = 2, Category = DeckCategory.MainDeck }
            }
        };

        _deckRepo.GetByIdAsync(deck.Id, Arg.Any<CancellationToken>()).Returns(deck);
        _cardRepo.GetByIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card> { cardA, cardB });

        var result = await _handler.Handle(new GetDeckStatsQuery(deck.Id), CancellationToken.None);

        result.TotalPriceUsd.Should().Be(166.00m);
    }

    [Fact]
    public async Task Handle_CardsWithNoPrices_TotalPriceIsZero()
    {
        var card = new Card { Id = Guid.NewGuid(), Name = "Bolt", TypeLine = "Instant", PriceUsd = null, Rarity = "common", SetCode = "lea", SetName = "Alpha", ScryfallId = "a", OracleId = "a" };
        var deck = new Deck
        {
            Id = Guid.NewGuid(), Name = "Test", Format = Format.Legacy, UserId = Guid.NewGuid(),
            Entries = new List<DeckEntry>
            {
                new() { Id = Guid.NewGuid(), CardId = card.Id, Quantity = 4, Category = DeckCategory.MainDeck }
            }
        };

        _deckRepo.GetByIdAsync(deck.Id, Arg.Any<CancellationToken>()).Returns(deck);
        _cardRepo.GetByIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card> { card });

        var result = await _handler.Handle(new GetDeckStatsQuery(deck.Id), CancellationToken.None);

        result.TotalPriceUsd.Should().Be(0m);
    }

    [Fact]
    public async Task Handle_DeckNotFound_Throws()
    {
        _deckRepo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Deck?)null);

        var act = () => _handler.Handle(new GetDeckStatsQuery(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Handle_CalculatesAverageCmc()
    {
        var bolt = new Card { Id = Guid.NewGuid(), Name = "Bolt", Cmc = 1, TypeLine = "Instant", Rarity = "common", SetCode = "tst", SetName = "Test", ScryfallId = "a", OracleId = "a" };
        var jace = new Card { Id = Guid.NewGuid(), Name = "Jace", Cmc = 4, TypeLine = "Planeswalker", Rarity = "mythic", SetCode = "tst", SetName = "Test", ScryfallId = "b", OracleId = "b" };
        var deck = new Deck
        {
            Id = Guid.NewGuid(), Name = "Test", Format = Format.Modern, UserId = Guid.NewGuid(),
            Entries = new List<DeckEntry>
            {
                new() { Id = Guid.NewGuid(), CardId = bolt.Id, Quantity = 4, Category = DeckCategory.MainDeck },
                new() { Id = Guid.NewGuid(), CardId = jace.Id, Quantity = 2, Category = DeckCategory.MainDeck }
            }
        };

        _deckRepo.GetByIdAsync(deck.Id, Arg.Any<CancellationToken>()).Returns(deck);
        _cardRepo.GetByIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card> { bolt, jace });

        var result = await _handler.Handle(new GetDeckStatsQuery(deck.Id), CancellationToken.None);

        // (4*1 + 2*4) / 6 = 12/6 = 2.0
        result.AverageCmc.Should().BeApproximately(2.0, 0.01);
    }

    [Fact]
    public async Task Handle_AverageCmc_ExcludesLands()
    {
        var bolt = new Card { Id = Guid.NewGuid(), Name = "Bolt", Cmc = 1, TypeLine = "Instant", Rarity = "common", SetCode = "tst", SetName = "Test", ScryfallId = "a", OracleId = "a" };
        var mountain = new Card { Id = Guid.NewGuid(), Name = "Mountain", Cmc = 0, TypeLine = "Basic Land — Mountain", Rarity = "common", SetCode = "tst", SetName = "Test", ScryfallId = "b", OracleId = "b" };
        var deck = new Deck
        {
            Id = Guid.NewGuid(), Name = "Test", Format = Format.Modern, UserId = Guid.NewGuid(),
            Entries = new List<DeckEntry>
            {
                new() { Id = Guid.NewGuid(), CardId = bolt.Id, Quantity = 4, Category = DeckCategory.MainDeck },
                new() { Id = Guid.NewGuid(), CardId = mountain.Id, Quantity = 20, Category = DeckCategory.MainDeck }
            }
        };

        _deckRepo.GetByIdAsync(deck.Id, Arg.Any<CancellationToken>()).Returns(deck);
        _cardRepo.GetByIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card> { bolt, mountain });

        var result = await _handler.Handle(new GetDeckStatsQuery(deck.Id), CancellationToken.None);

        result.AverageCmc.Should().BeApproximately(1.0, 0.01);
    }

    [Fact]
    public async Task Handle_CalculatesLandAndSpellCounts()
    {
        var bolt = new Card { Id = Guid.NewGuid(), Name = "Bolt", Cmc = 1, TypeLine = "Instant", Rarity = "common", SetCode = "tst", SetName = "Test", ScryfallId = "a", OracleId = "a" };
        var mountain = new Card { Id = Guid.NewGuid(), Name = "Mountain", Cmc = 0, TypeLine = "Basic Land — Mountain", Rarity = "common", SetCode = "tst", SetName = "Test", ScryfallId = "b", OracleId = "b" };
        var deck = new Deck
        {
            Id = Guid.NewGuid(), Name = "Test", Format = Format.Modern, UserId = Guid.NewGuid(),
            Entries = new List<DeckEntry>
            {
                new() { Id = Guid.NewGuid(), CardId = bolt.Id, Quantity = 36, Category = DeckCategory.MainDeck },
                new() { Id = Guid.NewGuid(), CardId = mountain.Id, Quantity = 24, Category = DeckCategory.MainDeck }
            }
        };

        _deckRepo.GetByIdAsync(deck.Id, Arg.Any<CancellationToken>()).Returns(deck);
        _cardRepo.GetByIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card> { bolt, mountain });

        var result = await _handler.Handle(new GetDeckStatsQuery(deck.Id), CancellationToken.None);

        result.LandCount.Should().Be(24);
        result.SpellCount.Should().Be(36);
    }

    [Fact]
    public async Task Handle_CalculatesRarityBreakdown()
    {
        var common = new Card { Id = Guid.NewGuid(), Name = "Bolt", TypeLine = "Instant", Rarity = "common", SetCode = "tst", SetName = "Test", ScryfallId = "a", OracleId = "a" };
        var rare = new Card { Id = Guid.NewGuid(), Name = "Jace", TypeLine = "Planeswalker", Rarity = "rare", SetCode = "tst", SetName = "Test", ScryfallId = "b", OracleId = "b" };
        var deck = new Deck
        {
            Id = Guid.NewGuid(), Name = "Test", Format = Format.Modern, UserId = Guid.NewGuid(),
            Entries = new List<DeckEntry>
            {
                new() { Id = Guid.NewGuid(), CardId = common.Id, Quantity = 4, Category = DeckCategory.MainDeck },
                new() { Id = Guid.NewGuid(), CardId = rare.Id, Quantity = 2, Category = DeckCategory.MainDeck }
            }
        };

        _deckRepo.GetByIdAsync(deck.Id, Arg.Any<CancellationToken>()).Returns(deck);
        _cardRepo.GetByIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card> { common, rare });

        var result = await _handler.Handle(new GetDeckStatsQuery(deck.Id), CancellationToken.None);

        result.RarityBreakdown["common"].Should().Be(4);
        result.RarityBreakdown["rare"].Should().Be(2);
    }

    [Fact]
    public async Task Handle_MaybeboardExcludedFromTotalsAndPrice()
    {
        var bolt = new Card { Id = Guid.NewGuid(), Name = "Bolt", TypeLine = "Instant", PriceUsd = 2.00m, Rarity = "common", SetCode = "tst", SetName = "Test", ScryfallId = "a", OracleId = "a" };
        var maybe = new Card { Id = Guid.NewGuid(), Name = "Maybe", TypeLine = "Instant", PriceUsd = 10.00m, Rarity = "rare", SetCode = "tst", SetName = "Test", ScryfallId = "b", OracleId = "b" };
        var deck = new Deck
        {
            Id = Guid.NewGuid(), Name = "Test", Format = Format.Modern, UserId = Guid.NewGuid(),
            Entries = new List<DeckEntry>
            {
                new() { Id = Guid.NewGuid(), CardId = bolt.Id, Quantity = 4, Category = DeckCategory.MainDeck },
                new() { Id = Guid.NewGuid(), CardId = maybe.Id, Quantity = 3, Category = DeckCategory.Maybeboard }
            }
        };

        _deckRepo.GetByIdAsync(deck.Id, Arg.Any<CancellationToken>()).Returns(deck);
        _cardRepo.GetByIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card> { bolt, maybe });

        var result = await _handler.Handle(new GetDeckStatsQuery(deck.Id), CancellationToken.None);

        result.TotalCards.Should().Be(4); // Maybeboard excluded
        result.TotalPriceUsd.Should().Be(8.00m); // Only 4 bolts at $2
    }
}
