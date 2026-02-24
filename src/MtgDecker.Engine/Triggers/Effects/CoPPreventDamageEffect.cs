using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Triggers.Effects;

/// <summary>
/// Circle of Protection activated ability effect: prevent the next damage
/// from a source of the specified color to you this turn.
/// Simplified: adds a PreventDamageToPlayer continuous effect until end of turn.
/// </summary>
public class CoPPreventDamageEffect(ManaColor color) : IEffect
{
    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var effect = new ContinuousEffect(
            context.Source.Id,
            ContinuousEffectType.PreventDamageToPlayer,
            (_, _) => true,
            UntilEndOfTurn: true);
        context.State.ActiveEffects.Add(effect);
        context.State.Log($"{context.Source.Name} — {context.Controller.Name} gains damage prevention from {color} sources this turn.");
        return Task.CompletedTask;
    }
}
