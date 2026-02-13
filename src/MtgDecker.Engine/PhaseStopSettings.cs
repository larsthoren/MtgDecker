using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine;

public class PhaseStopSettings
{
    public HashSet<Phase> PhaseStops { get; } = [Phase.MainPhase1, Phase.MainPhase2];
    public HashSet<CombatStep> CombatStops { get; } = [CombatStep.DeclareAttackers, CombatStep.DeclareBlockers];

    public bool ShouldStop(Phase phase) => PhaseStops.Contains(phase);
    public bool ShouldStop(CombatStep step) => CombatStops.Contains(step);

    public void TogglePhase(Phase phase)
    {
        if (!PhaseStops.Remove(phase))
            PhaseStops.Add(phase);
    }

    public void ToggleCombatStep(CombatStep step)
    {
        if (!CombatStops.Remove(step))
            CombatStops.Add(step);
    }
}
