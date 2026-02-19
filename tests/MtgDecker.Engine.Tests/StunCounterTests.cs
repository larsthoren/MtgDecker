using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class StunCounterTests
{
    private GameEngine CreateEngine(out GameState state, out TestDecisionHandler p1Handler, out TestDecisionHandler p2Handler)
    {
        p1Handler = new TestDecisionHandler();
        p2Handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "Alice", p1Handler);
        var p2 = new Player(Guid.NewGuid(), "Bob", p2Handler);
        state = new GameState(p1, p2);
        return new GameEngine(state);
    }

    [Fact]
    public void StunCounter_ExistsInEnum()
    {
        CounterType.Stun.Should().BeDefined();
    }

    [Fact]
    public void Creature_WithStunCounters_DoesNotUntap_RemovesOneCounter()
    {
        var engine = CreateEngine(out var state, out _, out _);

        var creature = new GameCard
        {
            Name = "Stunned Bear",
            CardTypes = CardType.Creature,
            BasePower = 2,
            BaseToughness = 2,
            IsTapped = true,
        };
        creature.AddCounters(CounterType.Stun, 2);
        state.ActivePlayer.Battlefield.Add(creature);

        engine.ExecuteTurnBasedAction(Phase.Untap);

        creature.IsTapped.Should().BeTrue("creature with stun counters should not untap");
        creature.GetCounters(CounterType.Stun).Should().Be(1, "one stun counter should be removed");
    }

    [Fact]
    public void Creature_WithOneStunCounter_UntapsNextTurnAfterCounterRemoved()
    {
        var engine = CreateEngine(out var state, out _, out _);

        var creature = new GameCard
        {
            Name = "Almost Free Bear",
            CardTypes = CardType.Creature,
            BasePower = 2,
            BaseToughness = 2,
            IsTapped = true,
        };
        creature.AddCounters(CounterType.Stun, 1);
        state.ActivePlayer.Battlefield.Add(creature);

        // First untap: removes stun counter, stays tapped
        engine.ExecuteTurnBasedAction(Phase.Untap);
        creature.IsTapped.Should().BeTrue("creature should stay tapped when stun counter is removed");
        creature.GetCounters(CounterType.Stun).Should().Be(0, "the single stun counter should be removed");

        // Re-tap for next untap step test (creature stays tapped from stun, but let's be explicit)
        // Actually creature is already tapped, so next untap should work

        // Second untap: no stun counters, untaps normally
        engine.ExecuteTurnBasedAction(Phase.Untap);
        creature.IsTapped.Should().BeFalse("creature without stun counters should untap normally");
    }

    [Fact]
    public void Creature_WithoutStunCounter_UntapsNormally()
    {
        var engine = CreateEngine(out var state, out _, out _);

        var creature = new GameCard
        {
            Name = "Normal Bear",
            CardTypes = CardType.Creature,
            BasePower = 2,
            BaseToughness = 2,
            IsTapped = true,
        };
        state.ActivePlayer.Battlefield.Add(creature);

        engine.ExecuteTurnBasedAction(Phase.Untap);
        creature.IsTapped.Should().BeFalse("creature without stun counters should untap normally");
    }

    [Fact]
    public void Land_WithStunCounter_DoesNotUntap_RemovesCounter()
    {
        var engine = CreateEngine(out var state, out _, out _);

        var land = new GameCard
        {
            Name = "Stunned Forest",
            CardTypes = CardType.Land,
            IsTapped = true,
        };
        land.AddCounters(CounterType.Stun, 1);
        state.ActivePlayer.Battlefield.Add(land);

        engine.ExecuteTurnBasedAction(Phase.Untap);

        land.IsTapped.Should().BeTrue("land with stun counter should not untap");
        land.GetCounters(CounterType.Stun).Should().Be(0, "the stun counter should be removed");
    }

    [Fact]
    public void UntappedCard_WithStunCounter_StillRemovesCounterDuringUntapStep()
    {
        // Per MTG rules, stun counters are removed during untap step even if the permanent
        // is already untapped (the "would untap" replacement still applies)
        var engine = CreateEngine(out var state, out _, out _);

        var creature = new GameCard
        {
            Name = "Untapped Stunned Bear",
            CardTypes = CardType.Creature,
            BasePower = 2,
            BaseToughness = 2,
            IsTapped = false, // already untapped
        };
        creature.AddCounters(CounterType.Stun, 1);
        state.ActivePlayer.Battlefield.Add(creature);

        engine.ExecuteTurnBasedAction(Phase.Untap);

        creature.IsTapped.Should().BeFalse("already untapped creature should stay untapped");
        creature.GetCounters(CounterType.Stun).Should().Be(0, "stun counter should be removed during untap step");
    }

    [Fact]
    public void StunCounter_LogsRemoval()
    {
        var engine = CreateEngine(out var state, out _, out _);

        var creature = new GameCard
        {
            Name = "Stunned Bear",
            CardTypes = CardType.Creature,
            BasePower = 2,
            BaseToughness = 2,
            IsTapped = true,
        };
        creature.AddCounters(CounterType.Stun, 1);
        state.ActivePlayer.Battlefield.Add(creature);

        engine.ExecuteTurnBasedAction(Phase.Untap);

        state.GameLog.Should().Contain(log =>
            log.Contains("stun counter") && log.Contains("Stunned Bear"));
    }
}
