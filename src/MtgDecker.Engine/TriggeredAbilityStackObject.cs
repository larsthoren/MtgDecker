using MtgDecker.Engine.Triggers;

namespace MtgDecker.Engine;

public class TriggeredAbilityStackObject : IStackObject
{
    public Guid Id { get; } = Guid.NewGuid();
    public GameCard Source { get; }
    public Guid ControllerId { get; }
    public IEffect Effect { get; }
    public GameCard? Target { get; init; }
    public Guid? TargetPlayerId { get; init; }

    public TriggeredAbilityStackObject(GameCard source, Guid controllerId, IEffect effect, GameCard? target = null)
    {
        Source = source;
        ControllerId = controllerId;
        Effect = effect;
        Target = target;
    }
}
