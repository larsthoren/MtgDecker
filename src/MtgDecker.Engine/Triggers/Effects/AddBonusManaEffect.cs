using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Triggers.Effects;

public class AddBonusManaEffect(ManaColor color) : IEffect
{
    public ManaColor Color { get; } = color;

    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        context.Controller.ManaPool.Add(Color);
        context.State.Log($"Wild Growth adds {Color} mana.");
        return Task.CompletedTask;
    }
}
