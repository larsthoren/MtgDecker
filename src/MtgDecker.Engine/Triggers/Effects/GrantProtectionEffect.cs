using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine.Triggers.Effects;

public class GrantProtectionEffect : IEffect
{
    private static readonly ManaColor[] ProtectionColors =
        [ManaColor.White, ManaColor.Blue, ManaColor.Black, ManaColor.Red, ManaColor.Green];

    public async Task Execute(EffectContext context, CancellationToken ct = default)
    {
        if (context.Target == null) return;

        var color = await context.DecisionHandler.ChooseManaColor(ProtectionColors, ct);

        var targetId = context.Target.Id;
        var protection = new ContinuousEffect(
            context.Source.Id,
            ContinuousEffectType.GrantKeyword,
            (card, _) => card.Id == targetId,
            GrantedKeyword: Keyword.Protection,
            UntilEndOfTurn: true);
        context.State.ActiveEffects.Add(protection);

        context.State.Log($"{context.Target.Name} gains protection from {color} until end of turn.");
    }
}
