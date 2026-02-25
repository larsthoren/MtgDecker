using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine;

public class StackObject : IStackObject
{
    public Guid Id { get; } = Guid.NewGuid();
    public GameCard Card { get; }
    public Guid ControllerId { get; }
    public IReadOnlyDictionary<ManaColor, int> ManaPaid { get; }
    public IReadOnlyList<TargetInfo> Targets { get; }
    public int Timestamp { get; }
    public bool IsFlashback { get; init; }
    public bool IsMadness { get; init; }
    public bool IsAdventure { get; init; }
    public bool IsKicked { get; init; }
    public int? XValue { get; init; }

    public StackObject(GameCard card, Guid controllerId, Dictionary<ManaColor, int> manaPaid, List<TargetInfo> targets, int timestamp)
    {
        Card = card;
        ControllerId = controllerId;
        ManaPaid = manaPaid;
        Targets = targets.AsReadOnly();
        Timestamp = timestamp;
    }
}
