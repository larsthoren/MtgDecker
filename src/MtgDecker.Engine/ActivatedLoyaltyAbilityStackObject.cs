using MtgDecker.Engine.Triggers;

namespace MtgDecker.Engine;

public class ActivatedLoyaltyAbilityStackObject : IStackObject
{
    public Guid Id { get; } = Guid.NewGuid();
    public GameCard Source { get; }
    public Guid ControllerId { get; }
    public IEffect Effect { get; }
    public string Description { get; }
    public GameCard? Target { get; init; }
    public Guid? TargetPlayerId { get; init; }

    public ActivatedLoyaltyAbilityStackObject(GameCard source, Guid controllerId, IEffect effect, string description)
    {
        Source = source;
        ControllerId = controllerId;
        Effect = effect;
        Description = description;
    }
}
