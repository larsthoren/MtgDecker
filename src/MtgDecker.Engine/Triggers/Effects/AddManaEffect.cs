using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Triggers.Effects;

public class AddManaEffect(ManaColor color) : IEffect
{
    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        context.Controller.ManaPool.Add(color);
        context.State.Log($"{context.Controller.Name} adds {color} mana.");

        return Task.CompletedTask;
    }
}
