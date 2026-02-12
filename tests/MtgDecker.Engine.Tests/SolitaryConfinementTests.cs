using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class SolitaryConfinementTests
{
    [Fact]
    public void Is_Registered_In_CardDefinitions()
    {
        CardDefinitions.TryGet("Solitary Confinement", out var def).Should().BeTrue();
        def!.CardTypes.Should().Be(CardType.Enchantment);
        def.ManaCost.Should().NotBeNull();
    }

    [Fact]
    public void Has_SkipDraw_Effect()
    {
        CardDefinitions.TryGet("Solitary Confinement", out var def).Should().BeTrue();
        def!.ContinuousEffects.Should().Contain(e => e.Type == ContinuousEffectType.SkipDraw);
    }

    [Fact]
    public void Has_PlayerShroud_And_DamageProtection()
    {
        CardDefinitions.TryGet("Solitary Confinement", out var def).Should().BeTrue();
        def!.ContinuousEffects.Should().Contain(e => e.Type == ContinuousEffectType.GrantPlayerShroud);
        def.ContinuousEffects.Should().Contain(e => e.Type == ContinuousEffectType.PreventDamageToPlayer);
    }

    [Fact]
    public void Has_Upkeep_Trigger()
    {
        CardDefinitions.TryGet("Solitary Confinement", out var def).Should().BeTrue();
        def!.Triggers.Should().ContainSingle();
        var trigger = def.Triggers[0];
        trigger.Event.Should().Be(GameEvent.Upkeep);
        trigger.Condition.Should().Be(TriggerCondition.Upkeep);
        trigger.Effect.Should().BeOfType<UpkeepCostEffect>();
    }

    [Fact]
    public void RecalculateState_Applies_All_Effects()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var confinement = GameCard.Create("Solitary Confinement");
        p1.Battlefield.Add(confinement);

        engine.RecalculateState();

        // All three continuous effects should be in ActiveEffects
        state.ActiveEffects.Should().Contain(e =>
            e.Type == ContinuousEffectType.SkipDraw && e.SourceId == confinement.Id);
        state.ActiveEffects.Should().Contain(e =>
            e.Type == ContinuousEffectType.GrantPlayerShroud && e.SourceId == confinement.Id);
        state.ActiveEffects.Should().Contain(e =>
            e.Type == ContinuousEffectType.PreventDamageToPlayer && e.SourceId == confinement.Id);
    }
}
