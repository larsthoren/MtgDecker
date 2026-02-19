namespace MtgDecker.Engine;

public class CombatState
{
    public Guid AttackingPlayerId { get; }
    public Guid DefendingPlayerId { get; }
    private readonly List<Guid> _attackers = new();
    public IReadOnlyList<Guid> Attackers => _attackers;

    private readonly Dictionary<Guid, Guid?> _attackerTargets = new(); // attackerId -> planeswalker target (null = player)
    private readonly Dictionary<Guid, List<Guid>> _blockerAssignments = new(); // attackerId -> blockerIds
    private readonly Dictionary<Guid, List<Guid>> _blockerOrder = new(); // attackerId -> ordered blockerIds

    public CombatState(Guid attackingPlayerId, Guid defendingPlayerId)
    {
        AttackingPlayerId = attackingPlayerId;
        DefendingPlayerId = defendingPlayerId;
    }

    public void DeclareAttacker(Guid cardId)
    {
        if (!_attackers.Contains(cardId))
            _attackers.Add(cardId);
    }

    public bool RemoveAttacker(Guid cardId) => _attackers.Remove(cardId);

    public void SetAttackerTarget(Guid attackerId, Guid? planeswalkerTargetId)
    {
        _attackerTargets[attackerId] = planeswalkerTargetId;
    }

    public Guid? GetAttackerTarget(Guid attackerId)
    {
        return _attackerTargets.TryGetValue(attackerId, out var target) ? target : null;
    }

    public void DeclareBlocker(Guid blockerId, Guid attackerId)
    {
        if (!_blockerAssignments.ContainsKey(attackerId))
            _blockerAssignments[attackerId] = new List<Guid>();
        if (!_blockerAssignments[attackerId].Contains(blockerId))
            _blockerAssignments[attackerId].Add(blockerId);
    }

    public IReadOnlyList<Guid> GetBlockers(Guid attackerId) =>
        _blockerAssignments.TryGetValue(attackerId, out var blockers) ? blockers : [];

    public bool IsBlocked(Guid attackerId) =>
        _blockerAssignments.TryGetValue(attackerId, out var blockers) && blockers.Count > 0;

    public void SetBlockerOrder(Guid attackerId, List<Guid> orderedBlockerIds)
    {
        _blockerOrder[attackerId] = orderedBlockerIds;
    }

    public IReadOnlyList<Guid> GetBlockerOrder(Guid attackerId)
    {
        if (_blockerOrder.TryGetValue(attackerId, out var order))
            return order;
        // Default: return blockers in declaration order
        return GetBlockers(attackerId);
    }
}
