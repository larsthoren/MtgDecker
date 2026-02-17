namespace MtgDecker.Engine.Triggers.Effects;

public class ProwessEffect : IEffect
{
    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var card = context.Source;

        // Add +1/+1 until end of turn
        var effect = new ContinuousEffect(
            card.Id,
            ContinuousEffectType.ModifyPowerToughness,
            (c, _) => c.Id == card.Id,
            PowerMod: 1, ToughnessMod: 1,
            UntilEndOfTurn: true);
        context.State.ActiveEffects.Add(effect);

        context.State.Log($"{card.Name} gets +1/+1 until end of turn (prowess).");
        return Task.CompletedTask;
    }
}
