namespace MtgDecker.Engine;

public class CombatState
{
    public Guid AttackingPlayerId { get; }
    public Guid DefendingPlayerId { get; }
    public List<Guid> Attackers { get; } = new();

    private readonly Dictionary<Guid, List<Guid>> _blockerAssignments = new(); // attackerId -> blockerIds
    private readonly Dictionary<Guid, List<Guid>> _blockerOrder = new(); // attackerId -> ordered blockerIds

    public CombatState(Guid attackingPlayerId, Guid defendingPlayerId)
    {
        AttackingPlayerId = attackingPlayerId;
        DefendingPlayerId = defendingPlayerId;
    }

    public void DeclareAttacker(Guid cardId)
    {
        if (!Attackers.Contains(cardId))
            Attackers.Add(cardId);
    }

    public void DeclareBlocker(Guid blockerId, Guid attackerId)
    {
        if (!_blockerAssignments.ContainsKey(attackerId))
            _blockerAssignments[attackerId] = new List<Guid>();
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
