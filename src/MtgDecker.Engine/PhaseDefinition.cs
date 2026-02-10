using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine;

public class PhaseDefinition
{
    public Phase Phase { get; init; }
    public bool GrantsPriority { get; init; }
    public bool HasTurnBasedAction { get; init; }
}
