namespace MtgDecker.Engine.Triggers.Effects;

public class PumpSelfEffect(int powerMod, int toughnessMod) : IEffect
{
    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var sourceId = context.Source.Id;
        var effect = new ContinuousEffect(
            sourceId,
            ContinuousEffectType.ModifyPowerToughness,
            (card, _) => card.Id == sourceId,
            PowerMod: powerMod,
            ToughnessMod: toughnessMod,
            UntilEndOfTurn: true);
        context.State.ActiveEffects.Add(effect);
        context.State.Log($"{context.Source.Name} gets +{powerMod}/+{toughnessMod} until end of turn.");
        return Task.CompletedTask;
    }
}
