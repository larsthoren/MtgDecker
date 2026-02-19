using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Triggers.Effects;

public class AddAnyManaEffect : IEffect
{
    public async Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var colors = new List<ManaColor>
            { ManaColor.White, ManaColor.Blue, ManaColor.Black, ManaColor.Red, ManaColor.Green };

        var chosen = await context.DecisionHandler.ChooseManaColor(colors, ct);
        context.Controller.ManaPool.Add(chosen);
        context.State.Log($"{context.Controller.Name} adds {chosen} mana.");
    }
}
