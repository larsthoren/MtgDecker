using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Triggers.Effects;

public class WeakenTargetEffect(int powerMod, int toughnessMod) : IEffect
{
    public int PowerMod { get; } = powerMod;
    public int ToughnessMod { get; } = toughnessMod;

    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        if (context.Target == null) return Task.CompletedTask;

        var targetId = context.Target.Id;
        var effect = new ContinuousEffect(
            context.Source.Id,
            ContinuousEffectType.ModifyPowerToughness,
            (card, _) => card.Id == targetId,
            PowerMod: PowerMod,
            ToughnessMod: ToughnessMod,
            UntilEndOfTurn: true);
        context.State.ActiveEffects.Add(effect);

        context.State.Log($"{context.Target.Name} gets {PowerMod:+0;-#}/{ToughnessMod:+0;-#} until end of turn.");
        return Task.CompletedTask;
    }
}
