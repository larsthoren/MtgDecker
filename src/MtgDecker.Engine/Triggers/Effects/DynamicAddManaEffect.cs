using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Triggers.Effects;

public class DynamicAddManaEffect(ManaColor color, Func<Player, int> countFunc) : IEffect
{
    public ManaColor Color { get; } = color;
    public Func<Player, int> CountFunc { get; } = countFunc;

    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var amount = CountFunc(context.Controller);
        if (amount > 0)
        {
            context.Controller.ManaPool.Add(Color, amount);
            context.State.Log($"{context.Controller.Name} adds {amount} {Color} mana from {context.Source.Name}.");
        }
        else
        {
            context.State.Log($"{context.Source.Name} produces no mana.");
        }
        return Task.CompletedTask;
    }
}
