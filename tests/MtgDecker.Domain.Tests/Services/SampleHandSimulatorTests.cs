using FluentAssertions;
using MtgDecker.Domain.Services;

namespace MtgDecker.Domain.Tests.Services;

public class SampleHandSimulatorTests
{
    [Fact]
    public void NewGame_DrawsSevenCards()
    {
        var library = CreateLibrary(60);
        var sim = new SampleHandSimulator(library);

        sim.NewGame();

        sim.Hand.Should().HaveCount(7);
        sim.LibraryCount.Should().Be(53);
    }

    [Fact]
    public void DrawCard_AddsOneCardToHand()
    {
        var library = CreateLibrary(60);
        var sim = new SampleHandSimulator(library);
        sim.NewGame();

        sim.DrawCard();

        sim.Hand.Should().HaveCount(8);
        sim.LibraryCount.Should().Be(52);
    }

    [Fact]
    public void DrawCard_EmptyLibrary_ReturnsFalse()
    {
        var library = CreateLibrary(7);
        var sim = new SampleHandSimulator(library);
        sim.NewGame();

        var result = sim.DrawCard();

        result.Should().BeFalse();
        sim.Hand.Should().HaveCount(7);
    }

    [Fact]
    public void Mulligan_FirstMulligan_DrawsSixCards()
    {
        var library = CreateLibrary(60);
        var sim = new SampleHandSimulator(library);
        sim.NewGame();

        sim.Mulligan();

        sim.Hand.Should().HaveCount(6);
        sim.MulliganCount.Should().Be(1);
        sim.LibraryCount.Should().Be(54);
    }

    [Fact]
    public void Mulligan_SecondMulligan_DrawsFiveCards()
    {
        var library = CreateLibrary(60);
        var sim = new SampleHandSimulator(library);
        sim.NewGame();
        sim.Mulligan();

        sim.Mulligan();

        sim.Hand.Should().HaveCount(5);
        sim.MulliganCount.Should().Be(2);
    }

    [Fact]
    public void Mulligan_SixthMulligan_DrawsOneCard()
    {
        var library = CreateLibrary(60);
        var sim = new SampleHandSimulator(library);
        sim.NewGame();
        for (int i = 0; i < 5; i++) sim.Mulligan();

        sim.Mulligan();

        sim.Hand.Should().HaveCount(1);
        sim.MulliganCount.Should().Be(6);
    }

    [Fact]
    public void Mulligan_AtMinimumHand_CannotMulliganFurther()
    {
        var library = CreateLibrary(60);
        var sim = new SampleHandSimulator(library);
        sim.NewGame();
        for (int i = 0; i < 6; i++) sim.Mulligan();

        var result = sim.Mulligan();

        result.Should().BeFalse();
        sim.Hand.Should().HaveCount(1);
    }

    [Fact]
    public void NewGame_Reshuffles_ResetsState()
    {
        var library = CreateLibrary(60);
        var sim = new SampleHandSimulator(library);
        sim.NewGame();
        sim.Mulligan();
        sim.DrawCard();

        sim.NewGame();

        sim.Hand.Should().HaveCount(7);
        sim.MulliganCount.Should().Be(0);
        sim.LibraryCount.Should().Be(53);
    }

    [Fact]
    public void Hand_ContainsOnlyCardsFromLibrary()
    {
        var cardIds = new List<Guid>();
        for (int i = 0; i < 60; i++)
            cardIds.Add(Guid.NewGuid());

        var sim = new SampleHandSimulator(cardIds);
        sim.NewGame();

        sim.Hand.Should().OnlyContain(id => cardIds.Contains(id));
    }

    [Fact]
    public void FromDeckEntries_ExpandsQuantities()
    {
        var entries = new List<(Guid CardId, int Quantity)>();
        for (int i = 0; i < 15; i++)
            entries.Add((Guid.NewGuid(), 4));

        var sim = SampleHandSimulator.FromDeckEntries(entries);
        sim.NewGame();

        sim.Hand.Should().HaveCount(7);
        sim.LibraryCount.Should().Be(53);
    }

    private static List<Guid> CreateLibrary(int count)
    {
        return Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToList();
    }
}
