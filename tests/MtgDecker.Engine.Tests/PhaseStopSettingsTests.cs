using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Tests;

public class PhaseStopSettingsTests
{
    [Fact]
    public void DefaultStops_IncludeMainPhasesAndCombat()
    {
        var settings = new PhaseStopSettings();
        settings.ShouldStop(Phase.MainPhase1).Should().BeTrue();
        settings.ShouldStop(Phase.MainPhase2).Should().BeTrue();
        settings.ShouldStop(CombatStep.DeclareAttackers).Should().BeTrue();
        settings.ShouldStop(CombatStep.DeclareBlockers).Should().BeTrue();
    }

    [Fact]
    public void DefaultStops_ExcludeNonMainPhases()
    {
        var settings = new PhaseStopSettings();
        settings.ShouldStop(Phase.Untap).Should().BeFalse();
        settings.ShouldStop(Phase.Upkeep).Should().BeFalse();
        settings.ShouldStop(Phase.Draw).Should().BeFalse();
        settings.ShouldStop(Phase.End).Should().BeFalse();
        settings.ShouldStop(CombatStep.BeginCombat).Should().BeFalse();
        settings.ShouldStop(CombatStep.CombatDamage).Should().BeFalse();
        settings.ShouldStop(CombatStep.EndCombat).Should().BeFalse();
    }

    [Fact]
    public void TogglePhase_TogglesOnAndOff()
    {
        var settings = new PhaseStopSettings();
        settings.TogglePhase(Phase.Upkeep);
        settings.ShouldStop(Phase.Upkeep).Should().BeTrue();
        settings.TogglePhase(Phase.Upkeep);
        settings.ShouldStop(Phase.Upkeep).Should().BeFalse();
    }

    [Fact]
    public void ToggleCombatStep_TogglesOnAndOff()
    {
        var settings = new PhaseStopSettings();
        settings.ToggleCombatStep(CombatStep.BeginCombat);
        settings.ShouldStop(CombatStep.BeginCombat).Should().BeTrue();
        settings.ToggleCombatStep(CombatStep.BeginCombat);
        settings.ShouldStop(CombatStep.BeginCombat).Should().BeFalse();
    }

    [Fact]
    public void ShouldStop_CombatPhase_ReturnsFalse()
    {
        var settings = new PhaseStopSettings();
        settings.ShouldStop(Phase.Combat).Should().BeFalse();
    }

    [Fact]
    public void InteractiveHandler_AutoPasses_WhenNoStop()
    {
        var handler = new InteractiveDecisionHandler();
        handler.ShouldAutoPass(Phase.Draw, CombatStep.None, stackEmpty: true).Should().BeTrue();
    }

    [Fact]
    public void InteractiveHandler_DoesNotAutoPass_WhenStopSet()
    {
        var handler = new InteractiveDecisionHandler();
        handler.ShouldAutoPass(Phase.MainPhase1, CombatStep.None, stackEmpty: true).Should().BeFalse();
    }

    [Fact]
    public void InteractiveHandler_DoesNotAutoPass_WhenStackHasItems()
    {
        var handler = new InteractiveDecisionHandler();
        handler.ShouldAutoPass(Phase.Draw, CombatStep.None, stackEmpty: false).Should().BeFalse();
    }

    [Fact]
    public void InteractiveHandler_ChecksCombatStops()
    {
        var handler = new InteractiveDecisionHandler();
        handler.ShouldAutoPass(Phase.Combat, CombatStep.DeclareAttackers, stackEmpty: true).Should().BeFalse();
        handler.ShouldAutoPass(Phase.Combat, CombatStep.CombatDamage, stackEmpty: true).Should().BeTrue();
    }
}
