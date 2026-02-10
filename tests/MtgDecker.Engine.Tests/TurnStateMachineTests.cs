using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Tests;

public class TurnStateMachineTests
{
    [Fact]
    public void PhaseSequence_IsCorrectOrder()
    {
        var expected = new[]
        {
            Phase.Untap, Phase.Upkeep, Phase.Draw,
            Phase.MainPhase1, Phase.Combat,
            Phase.MainPhase2, Phase.End
        };

        var sequence = TurnStateMachine.GetPhaseSequence()
            .Select(p => p.Phase)
            .ToList();

        sequence.Should().Equal(expected);
    }

    [Fact]
    public void CurrentPhase_StartsAtUntap()
    {
        var machine = new TurnStateMachine();
        machine.CurrentPhase.Phase.Should().Be(Phase.Untap);
    }

    [Fact]
    public void AdvancePhase_WalksThroughAllPhases()
    {
        var machine = new TurnStateMachine();
        var phases = new List<Phase> { machine.CurrentPhase.Phase };

        while (machine.AdvancePhase() != null)
            phases.Add(machine.CurrentPhase.Phase);

        phases.Should().Equal(
            Phase.Untap, Phase.Upkeep, Phase.Draw,
            Phase.MainPhase1, Phase.Combat,
            Phase.MainPhase2, Phase.End);
    }

    [Fact]
    public void AdvancePhase_ReturnsNull_WhenTurnEnds()
    {
        var machine = new TurnStateMachine();

        for (int i = 0; i < 6; i++)
            machine.AdvancePhase().Should().NotBeNull();

        machine.AdvancePhase().Should().BeNull();
    }

    [Fact]
    public void Reset_GoesBackToUntap()
    {
        var machine = new TurnStateMachine();
        machine.AdvancePhase();
        machine.AdvancePhase();

        machine.Reset();

        machine.CurrentPhase.Phase.Should().Be(Phase.Untap);
    }

    [Fact]
    public void UntapPhase_DoesNotGrantPriority()
    {
        var untap = TurnStateMachine.GetPhaseSequence()
            .First(p => p.Phase == Phase.Untap);

        untap.GrantsPriority.Should().BeFalse();
    }

    [Theory]
    [InlineData(Phase.Upkeep)]
    [InlineData(Phase.Draw)]
    [InlineData(Phase.MainPhase1)]
    [InlineData(Phase.Combat)]
    [InlineData(Phase.MainPhase2)]
    [InlineData(Phase.End)]
    public void NonUntapPhases_GrantPriority(Phase phase)
    {
        var phaseDef = TurnStateMachine.GetPhaseSequence()
            .First(p => p.Phase == phase);

        phaseDef.GrantsPriority.Should().BeTrue();
    }

    [Theory]
    [InlineData(Phase.Untap, true)]
    [InlineData(Phase.Draw, true)]
    [InlineData(Phase.Upkeep, false)]
    [InlineData(Phase.MainPhase1, false)]
    [InlineData(Phase.Combat, false)]
    [InlineData(Phase.MainPhase2, false)]
    [InlineData(Phase.End, false)]
    public void HasTurnBasedAction_CorrectPerPhase(Phase phase, bool expected)
    {
        var phaseDef = TurnStateMachine.GetPhaseSequence()
            .First(p => p.Phase == phase);

        phaseDef.HasTurnBasedAction.Should().Be(expected);
    }
}
