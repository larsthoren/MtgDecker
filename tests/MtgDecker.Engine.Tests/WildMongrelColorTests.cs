using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers.Effects;

// ReSharper disable InconsistentNaming

namespace MtgDecker.Engine.Tests;

public class WildMongrelColorTests
{
    [Fact]
    public async Task WildMongrelEffect_PumpsAndChangesColor()
    {
        var h1 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);

        var mongrel = GameCard.Create("Wild Mongrel");
        p1.Battlefield.Add(mongrel);

        // Choose green color
        h1.EnqueueManaColor(ManaColor.Green);

        var ctx = new MtgDecker.Engine.Triggers.EffectContext(state, p1, mongrel, h1);
        var effect = new WildMongrelEffect();
        await effect.Execute(ctx);

        // Should have pump effect
        state.ActiveEffects.Should().HaveCount(1);
        state.ActiveEffects[0].PowerMod.Should().Be(1);
        state.ActiveEffects[0].ToughnessMod.Should().Be(1);

        // Should be green only (lost its original green from ManaCost, but now has chosen green)
        mongrel.Colors.Should().HaveCount(1);
        mongrel.Colors.Should().Contain(ManaColor.Green);
    }

    [Fact]
    public async Task WildMongrelEffect_CanBecomeBlue()
    {
        var h1 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);

        var mongrel = GameCard.Create("Wild Mongrel");
        p1.Battlefield.Add(mongrel);

        // Choose blue
        h1.EnqueueManaColor(ManaColor.Blue);

        var ctx = new MtgDecker.Engine.Triggers.EffectContext(state, p1, mongrel, h1);
        await new WildMongrelEffect().Execute(ctx);

        mongrel.Colors.Should().HaveCount(1);
        mongrel.Colors.Should().Contain(ManaColor.Blue);
        mongrel.Colors.Should().NotContain(ManaColor.Green);
    }

    [Fact]
    public async Task WildMongrelEffect_ColorRestoredByDelayedTrigger()
    {
        var h1 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);

        var mongrel = GameCard.Create("Wild Mongrel");
        p1.Battlefield.Add(mongrel);

        // Original colors should be green
        mongrel.Colors.Should().Contain(ManaColor.Green);

        // Choose blue
        h1.EnqueueManaColor(ManaColor.Blue);

        var ctx = new MtgDecker.Engine.Triggers.EffectContext(state, p1, mongrel, h1);
        await new WildMongrelEffect().Execute(ctx);

        // Now blue
        mongrel.Colors.Should().Contain(ManaColor.Blue);
        mongrel.Colors.Should().NotContain(ManaColor.Green);

        // Delayed trigger should exist
        state.DelayedTriggers.Should().HaveCount(1);
        state.DelayedTriggers[0].FireOn.Should().Be(GameEvent.EndStep);

        // Simulate firing the delayed trigger (restore colors)
        var restoreCtx = new MtgDecker.Engine.Triggers.EffectContext(state, p1, mongrel, h1);
        await state.DelayedTriggers[0].Effect.Execute(restoreCtx);

        // Should be back to green
        mongrel.Colors.Should().Contain(ManaColor.Green);
        mongrel.Colors.Should().NotContain(ManaColor.Blue);
    }

    [Fact]
    public void WildMongrel_IsRegistered_WithWildMongrelEffect()
    {
        CardDefinitions.TryGet("Wild Mongrel", out var def).Should().BeTrue();
        def!.ActivatedAbilities.Should().HaveCount(1);
        def.ActivatedAbilities[0].Effect.Should().BeOfType<WildMongrelEffect>();
    }
}
