using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Triggers.Effects;

/// <summary>
/// Swaps the source creature's power and toughness until end of turn
/// by setting base P/T to the opposite values via a ContinuousEffect.
/// </summary>
public class SwapPowerToughnessEffect : IEffect
{
    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var source = context.Source;

        // Get current base values
        var currentPower = source.BasePower ?? 0;
        var currentToughness = source.BaseToughness ?? 0;

        var sourceId = source.Id;
        var effect = new ContinuousEffect(
            sourceId,
            ContinuousEffectType.SetBasePowerToughness,
            (card, _) => card.Id == sourceId,
            SetPower: currentToughness,
            SetToughness: currentPower,
            UntilEndOfTurn: true,
            Layer: EffectLayer.Layer7b_SetPT);
        context.State.ActiveEffects.Add(effect);
        context.State.Log($"{source.Name}'s power and toughness are switched until end of turn ({currentToughness}/{currentPower}).");
        return Task.CompletedTask;
    }
}
