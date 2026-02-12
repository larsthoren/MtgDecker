using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class CounterTests
{
    [Fact]
    public void AddCounters_PlacesCounters()
    {
        var card = new GameCard { Name = "Test Card" };

        card.AddCounters(CounterType.Fade, 5);

        card.GetCounters(CounterType.Fade).Should().Be(5);
    }

    [Fact]
    public void AddCounters_StacksWithExisting()
    {
        var card = new GameCard { Name = "Test Card" };

        card.AddCounters(CounterType.Fade, 3);
        card.AddCounters(CounterType.Fade, 2);

        card.GetCounters(CounterType.Fade).Should().Be(5);
    }

    [Fact]
    public void RemoveCounter_DecrementsAndReturnsTrue()
    {
        var card = new GameCard { Name = "Test Card" };
        card.AddCounters(CounterType.Fade, 3);

        var result = card.RemoveCounter(CounterType.Fade);

        result.Should().BeTrue();
        card.GetCounters(CounterType.Fade).Should().Be(2);
    }

    [Fact]
    public void RemoveCounter_ReturnsFalseWhenNoCounters()
    {
        var card = new GameCard { Name = "Test Card" };

        var result = card.RemoveCounter(CounterType.Fade);

        result.Should().BeFalse();
    }

    [Fact]
    public void GetCounters_ReturnsZeroForUnknownType()
    {
        var card = new GameCard { Name = "Test Card" };

        card.GetCounters(CounterType.Fade).Should().Be(0);
    }

    [Fact]
    public async Task AddCountersEffect_PlacesCountersOnSource()
    {
        var card = new GameCard { Name = "Test Enchantment" };
        var handler = new TestDecisionHandler();
        var state = new GameState(
            new Player(Guid.NewGuid(), "P1", handler),
            new Player(Guid.NewGuid(), "P2", handler));
        var context = new EffectContext(state, state.Player1, card, handler);

        var effect = new AddCountersEffect(CounterType.Fade, 5);
        await effect.Execute(context);

        card.GetCounters(CounterType.Fade).Should().Be(5);
    }

    [Fact]
    public async Task RemoveCounterCost_PreventsActivationWithNoCounters()
    {
        // Setup: a card with an activated ability that costs removing a Fade counter
        var handler = new TestDecisionHandler();
        var state = new GameState(
            new Player(Guid.NewGuid(), "P1", handler),
            new Player(Guid.NewGuid(), "P2", handler));

        var wave = GameCard.Create("Parallax Wave", "Enchantment");
        // Don't add any counters — activation should fail
        state.Player1.Battlefield.Add(wave);

        var engine = new GameEngine(state);

        await engine.ExecuteAction(GameAction.ActivateAbility(state.Player1.Id, wave.Id));

        // The log should indicate it can't activate
        state.GameLog.Should().Contain(l => l.Contains("no Fade counters"));
    }
}
