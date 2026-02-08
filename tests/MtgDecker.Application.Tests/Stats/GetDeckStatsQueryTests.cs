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
        deck.AddCard(bolt, 4, DeckCategory.MainDeck);
        deck.AddCard(snap, 3, DeckCategory.MainDeck);

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
        deck.AddCard(land, 20, DeckCategory.MainDeck);

        _deckRepo.GetByIdAsync(deck.Id, Arg.Any<CancellationToken>()).Returns(deck);
        _cardRepo.GetByIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card> { land });

        var result = await _handler.Handle(new GetDeckStatsQuery(deck.Id), CancellationToken.None);

        result.ManaCurve.Should().BeEmpty();
        result.TypeBreakdown.Should().ContainKey("Land");
    }

    [Fact]
    public async Task Handle_DeckNotFound_Throws()
    {
        _deckRepo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Deck?)null);

        var act = () => _handler.Handle(new GetDeckStatsQuery(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
