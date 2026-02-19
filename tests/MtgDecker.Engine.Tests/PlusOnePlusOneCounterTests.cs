using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class PlusOnePlusOneCounterTests
{
    private static (GameEngine engine, GameState state, Player player) SetupGame()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);
        return (engine, state, p1);
    }

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

    [Fact]
    public void RecalculateState_CreatureWithPlusOnePlusOneCounters_HasModifiedPT()
    {
        var (engine, state, player) = SetupGame();
        var creature = new GameCard
        {
            Name = "Test Creature",
            BasePower = 2,
            BaseToughness = 2,
            CardTypes = CardType.Creature,
        };
        creature.AddCounters(CounterType.PlusOnePlusOne, 3);
        player.Battlefield.Add(creature);

        engine.RecalculateState();

        creature.Power.Should().Be(5);  // 2 base + 3 counters
        creature.Toughness.Should().Be(5);
    }

    [Fact]
    public void RecalculateState_NonCreatureWithCounters_NoEffect()
    {
        var (engine, state, player) = SetupGame();
        var enchantment = new GameCard
        {
            Name = "Test Enchantment",
            CardTypes = CardType.Enchantment,
        };
        enchantment.AddCounters(CounterType.PlusOnePlusOne, 2);
        player.Battlefield.Add(enchantment);

        engine.RecalculateState();

        enchantment.Power.Should().BeNull();
        enchantment.Toughness.Should().BeNull();
    }
}
