using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;

// ReSharper disable InconsistentNaming

namespace MtgDecker.Engine.Tests;

/// <summary>
/// Tests for Spiritual Focus: only triggers on opponent-caused discards,
/// NOT on self-caused discards (Wild Mongrel, Careful Study, hand size).
/// </summary>
public class SpiritualFocusSourceTests
{
    [Fact]
    public void SpiritualFocus_DoesNotTrigger_OnSelfDiscard()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        // P1 controls Spiritual Focus
        var focus = GameCard.Create("Spiritual Focus");
        p1.Battlefield.Add(focus);

        // Self-caused discard
        state.LastDiscardCausedByPlayerId = p1.Id;
        state.ActivePlayer = p1;

        // Queue discard triggers
        engine.QueueDiscardTriggers(p1);

        // No trigger should fire
        state.StackCount.Should().Be(0);
    }

    [Fact]
    public void SpiritualFocus_Triggers_OnOpponentCausedDiscard()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        // P1 controls Spiritual Focus
        var focus = GameCard.Create("Spiritual Focus");
        p1.Battlefield.Add(focus);

        // Opponent-caused discard (P2 caused P1 to discard)
        state.LastDiscardCausedByPlayerId = p2.Id;
        state.ActivePlayer = p1;

        engine.QueueDiscardTriggers(p1);

        // Trigger should fire
        state.StackCount.Should().Be(1);
    }

    [Fact]
    public void SpiritualFocus_DoesNotTrigger_OnGameRuleDiscard()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var focus = GameCard.Create("Spiritual Focus");
        p1.Battlefield.Add(focus);

        // Game rule discard (hand size)
        state.LastDiscardCausedByPlayerId = null;
        state.ActivePlayer = p1;

        engine.QueueDiscardTriggers(p1);

        state.StackCount.Should().Be(0);
    }

    [Fact]
    public void SpiritualFocus_UsesOpponentCausesControllerDiscard_TriggerCondition()
    {
        CardDefinitions.TryGet("Spiritual Focus", out var def).Should().BeTrue();
        def!.Triggers.Should().HaveCount(1);
        def.Triggers[0].Condition.Should().Be(TriggerCondition.OpponentCausesControllerDiscard);
    }
}
