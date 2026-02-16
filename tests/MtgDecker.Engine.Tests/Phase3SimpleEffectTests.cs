using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class Phase3SimpleEffectTests
{
    private static (GameState state, Player p1, Player p2,
        TestDecisionHandler h1) Setup()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        var state = new GameState(p1, p2);
        return (state, p1, p2, h1);
    }

    // === GainLifeEffect ===

    [Fact]
    public async Task GainLifeEffect_IncreasesControllerLife()
    {
        var (state, p1, _, h1) = Setup();
        var source = new GameCard { Name = "Healer" };
        var context = new EffectContext(state, p1, source, h1);

        var effect = new GainLifeEffect(4);
        await effect.Execute(context);

        p1.Life.Should().Be(24, "started at 20, gained 4");
    }

    [Fact]
    public async Task GainLifeEffect_Works_WithDifferentAmounts()
    {
        var (state, p1, _, h1) = Setup();
        var context = new EffectContext(state, p1, new GameCard { Name = "Orb" }, h1);

        await new GainLifeEffect(2).Execute(context);
        p1.Life.Should().Be(22);

        await new GainLifeEffect(1).Execute(context);
        p1.Life.Should().Be(23);
    }

    // === PumpSelfEffect ===

    [Fact]
    public async Task PumpSelfEffect_AddsContinuousEffect_UntilEndOfTurn()
    {
        var (state, p1, _, h1) = Setup();
        var creature = new GameCard { Name = "Shade", BasePower = 2, BaseToughness = 1,
            CardTypes = CardType.Creature };
        p1.Battlefield.Add(creature);
        var context = new EffectContext(state, p1, creature, h1);

        var effect = new PumpSelfEffect(1, 1);
        await effect.Execute(context);

        state.ActiveEffects.Should().ContainSingle(e =>
            e.Type == ContinuousEffectType.ModifyPowerToughness
            && e.UntilEndOfTurn == true);
    }

    [Fact]
    public async Task PumpSelfEffect_StacksMultipleTimes()
    {
        var (state, p1, _, h1) = Setup();
        var creature = new GameCard { Name = "Shade", BasePower = 2, BaseToughness = 1,
            CardTypes = CardType.Creature };
        p1.Battlefield.Add(creature);
        var context = new EffectContext(state, p1, creature, h1);

        var effect = new PumpSelfEffect(1, 1);
        await effect.Execute(context);
        await effect.Execute(context);
        await effect.Execute(context);

        state.ActiveEffects.Count(e =>
            e.Type == ContinuousEffectType.ModifyPowerToughness).Should().Be(3,
            "each activation adds a separate continuous effect");
    }

    // === RegisterEndOfTurnSacrificeEffect + SacrificeSpecificCardEffect ===

    [Fact]
    public async Task RegisterEndOfTurnSacrifice_AddsDelayedTrigger()
    {
        var (state, p1, _, h1) = Setup();
        var creature = new GameCard { Name = "Ball Lightning" };
        p1.Battlefield.Add(creature);
        var context = new EffectContext(state, p1, creature, h1);

        var effect = new RegisterEndOfTurnSacrificeEffect();
        await effect.Execute(context);

        state.DelayedTriggers.Should().ContainSingle(d =>
            d.FireOn == GameEvent.EndStep
            && d.ControllerId == p1.Id);
    }

    [Fact]
    public async Task SacrificeSpecificCard_RemovesFromBattlefield()
    {
        var (state, p1, _, h1) = Setup();
        var creature = new GameCard { Name = "Ball Lightning" };
        p1.Battlefield.Add(creature);
        var context = new EffectContext(state, p1, new GameCard { Name = "Delayed Trigger" }, h1);

        var effect = new SacrificeSpecificCardEffect(creature.Id);
        await effect.Execute(context);

        p1.Battlefield.Cards.Should().NotContain(c => c.Id == creature.Id);
        p1.Graveyard.Cards.Should().Contain(c => c.Id == creature.Id);
    }

    [Fact]
    public async Task SacrificeSpecificCard_DoesNothing_IfCardAlreadyGone()
    {
        var (state, p1, _, h1) = Setup();
        var creature = new GameCard { Name = "Ball Lightning" };
        // Don't add to battlefield — card already gone
        var context = new EffectContext(state, p1, new GameCard { Name = "Delayed Trigger" }, h1);

        var effect = new SacrificeSpecificCardEffect(creature.Id);
        await effect.Execute(context); // should not throw

        p1.Battlefield.Count.Should().Be(0);
    }

    [Fact]
    public async Task SacrificeSpecificCard_CallsFireLeaveBattlefieldTriggers()
    {
        var (state, p1, _, h1) = Setup();
        var creature = new GameCard { Name = "Ball Lightning", CardTypes = CardType.Creature };
        p1.Battlefield.Add(creature);

        var triggerFired = false;
        var context = new EffectContext(state, p1, new GameCard { Name = "Delayed Trigger" }, h1)
        {
            FireLeaveBattlefieldTriggers = _ =>
            {
                triggerFired = true;
                return Task.CompletedTask;
            }
        };

        var effect = new SacrificeSpecificCardEffect(creature.Id);
        await effect.Execute(context);

        triggerFired.Should().BeTrue(
            "sacrificing should fire leave-battlefield triggers for 'dies' effects");
    }
}
