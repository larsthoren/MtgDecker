using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine;

public class TurnStateMachine
{
    private static readonly PhaseDefinition[] _phases =
    [
        new() { Phase = Phase.Untap, GrantsPriority = false, HasTurnBasedAction = true },
        new() { Phase = Phase.Upkeep, GrantsPriority = true, HasTurnBasedAction = false },
        new() { Phase = Phase.Draw, GrantsPriority = true, HasTurnBasedAction = true },
        new() { Phase = Phase.MainPhase1, GrantsPriority = true, HasTurnBasedAction = false },
        new() { Phase = Phase.Combat, GrantsPriority = true, HasTurnBasedAction = false },
        new() { Phase = Phase.MainPhase2, GrantsPriority = true, HasTurnBasedAction = false },
        new() { Phase = Phase.End, GrantsPriority = true, HasTurnBasedAction = false },
    ];

    private int _currentIndex;

    public PhaseDefinition CurrentPhase => _phases[_currentIndex];

    public PhaseDefinition? AdvancePhase()
    {
        _currentIndex++;
        if (_currentIndex >= _phases.Length)
        {
            _currentIndex = 0;
            return null;
        }
        return _phases[_currentIndex];
    }

    public void Reset() => _currentIndex = 0;

    public static IReadOnlyList<PhaseDefinition> GetPhaseSequence() => _phases;
}
