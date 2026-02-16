using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class Phase3TriggerWiringTests
{
    // === Ball Lightning ===

    [Fact]
    public void BallLightning_HasETBSacrificeTrigger()
    {
        CardDefinitions.TryGet("Ball Lightning", out var def);

        def!.Triggers.Should().Contain(t =>
            t.Event == GameEvent.EnterBattlefield
            && t.Condition == TriggerCondition.Self
            && t.Effect is RegisterEndOfTurnSacrificeEffect,
            "Ball Lightning should register end-of-turn sacrifice on ETB");
    }

    [Fact]
    public async Task BallLightning_ETB_RegistersDelayedSacrifice()
    {
        var h1 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var ball = GameCard.Create("Ball Lightning", "Creature \u2014 Elemental");
        p1.Battlefield.Add(ball);
        ball.TurnEnteredBattlefield = state.TurnNumber;

        await engine.QueueSelfTriggersOnStackAsync(GameEvent.EnterBattlefield, ball, p1);

        state.StackCount.Should().Be(1);

        // Resolve the ETB trigger
        while (state.StackCount > 0)
        {
            var top = state.StackPopTop();
            if (top is TriggeredAbilityStackObject triggered)
            {
                var context = new EffectContext(state, p1, triggered.Source, h1);
                await triggered.Effect.Execute(context);
            }
        }

        state.DelayedTriggers.Should().ContainSingle(d =>
            d.FireOn == GameEvent.EndStep);
    }

    [Fact]
    public async Task BallLightning_DelayedTrigger_SacrificesAtEndOfTurn()
    {
        var h1 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);

        var ball = GameCard.Create("Ball Lightning", "Creature \u2014 Elemental");
        p1.Battlefield.Add(ball);

        state.DelayedTriggers.Add(new DelayedTrigger(
            GameEvent.EndStep,
            new SacrificeSpecificCardEffect(ball.Id),
            p1.Id));

        var engine = new GameEngine(state);
        await engine.QueueDelayedTriggersOnStackAsync(GameEvent.EndStep);

        state.StackCount.Should().Be(1);

        var top = state.StackPopTop() as TriggeredAbilityStackObject;
        var context = new EffectContext(state, p1, top!.Source, h1);
        await top.Effect.Execute(context);

        p1.Battlefield.Cards.Should().NotContain(c => c.Name == "Ball Lightning");
        p1.Graveyard.Cards.Should().Contain(c => c.Name == "Ball Lightning");
    }

    // === Plague Spitter ===

    [Fact]
    public void PlagueSpitter_HasDiesTrigger()
    {
        CardDefinitions.TryGet("Plague Spitter", out var def);

        def!.Triggers.Should().Contain(t =>
            t.Condition == TriggerCondition.SelfLeavesBattlefield
            && t.Effect is DamageAllCreaturesTriggerEffect,
            "Plague Spitter should deal 1 damage to all creatures and players when it dies");
    }

    [Fact]
    public void PlagueSpitter_StillHasUpkeepTrigger()
    {
        CardDefinitions.TryGet("Plague Spitter", out var def);

        def!.Triggers.Should().Contain(t =>
            t.Event == GameEvent.Upkeep
            && t.Condition == TriggerCondition.Upkeep,
            "Plague Spitter should keep its upkeep trigger");
    }
}
