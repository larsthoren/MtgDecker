using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Tests;

public class PlusOnePlusOneCounterTests
{
    [Fact]
    public void GameCard_AddPlusOnePlusOneCounters_TracksCorrectly()
    {
        var card = new GameCard { Name = "Test Creature", BasePower = 2, BaseToughness = 2 };

        card.AddCounters(CounterType.PlusOnePlusOne, 3);

        card.GetCounters(CounterType.PlusOnePlusOne).Should().Be(3);
    }

    [Fact]
    public void GameCard_RemovePlusOnePlusOneCounter_Decrements()
    {
        var card = new GameCard { Name = "Test Creature", BasePower = 2, BaseToughness = 2 };
        card.AddCounters(CounterType.PlusOnePlusOne, 3);

        card.RemoveCounter(CounterType.PlusOnePlusOne).Should().BeTrue();
        card.GetCounters(CounterType.PlusOnePlusOne).Should().Be(2);
    }
}
