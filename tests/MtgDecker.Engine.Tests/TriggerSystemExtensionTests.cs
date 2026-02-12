using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Triggers;
using NSubstitute;

namespace MtgDecker.Engine.Tests;

public class TriggerSystemExtensionTests
{
    [Theory]
    [InlineData(TriggerCondition.Self)]
    [InlineData(TriggerCondition.AnyCreatureDies)]
    [InlineData(TriggerCondition.ControllerCasts)]
    [InlineData(TriggerCondition.ControllerCastsEnchantment)]
    [InlineData(TriggerCondition.SelfDealsCombatDamage)]
    [InlineData(TriggerCondition.SelfAttacks)]
    [InlineData(TriggerCondition.Upkeep)]
    public void TriggerCondition_Has_Expected_Values(TriggerCondition condition)
    {
        Enum.IsDefined(condition).Should().BeTrue();
    }

    [Fact]
    public void GameEvent_EndStep_Exists()
    {
        var endStep = GameEvent.EndStep;
        Enum.IsDefined(endStep).Should().BeTrue();
    }

    [Fact]
    public void DelayedTrigger_Record_Stores_FireOn_Event()
    {
        var effect = Substitute.For<IEffect>();
        var controllerId = Guid.NewGuid();

        var trigger = new DelayedTrigger(GameEvent.EndStep, effect, controllerId);

        trigger.FireOn.Should().Be(GameEvent.EndStep);
    }

    [Fact]
    public void DelayedTrigger_Record_Stores_Effect()
    {
        var effect = Substitute.For<IEffect>();
        var controllerId = Guid.NewGuid();

        var trigger = new DelayedTrigger(GameEvent.EndStep, effect, controllerId);

        trigger.Effect.Should().Be(effect);
    }

    [Fact]
    public void DelayedTrigger_Record_Stores_ControllerId()
    {
        var effect = Substitute.For<IEffect>();
        var controllerId = Guid.NewGuid();

        var trigger = new DelayedTrigger(GameEvent.Dies, effect, controllerId);

        trigger.ControllerId.Should().Be(controllerId);
    }

    [Fact]
    public void DelayedTrigger_Records_With_Same_Values_Are_Equal()
    {
        var effect = Substitute.For<IEffect>();
        var controllerId = Guid.NewGuid();

        var trigger1 = new DelayedTrigger(GameEvent.EndStep, effect, controllerId);
        var trigger2 = new DelayedTrigger(GameEvent.EndStep, effect, controllerId);

        trigger1.Should().Be(trigger2);
    }

    [Fact]
    public void GameState_DelayedTriggers_Is_Initialized_And_Empty()
    {
        var player1 = new Player(Guid.NewGuid(), "P1", Substitute.For<IPlayerDecisionHandler>());
        var player2 = new Player(Guid.NewGuid(), "P2", Substitute.For<IPlayerDecisionHandler>());
        var state = new GameState(player1, player2);

        state.DelayedTriggers.Should().NotBeNull();
        state.DelayedTriggers.Should().BeEmpty();
    }
}
