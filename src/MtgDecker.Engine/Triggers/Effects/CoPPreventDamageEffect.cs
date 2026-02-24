using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Triggers.Effects;

/// <summary>
/// Circle of Protection activated ability effect: prevent the next instance of damage
/// from a source of the specified color to you this turn.
/// Adds a single-use DamagePreventionShield to the controller.
/// </summary>
public class CoPPreventDamageEffect(ManaColor color) : IEffect
{
    public ManaColor Color => color;

    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        context.Controller.DamagePreventionShields.Add(new DamagePreventionShield(color));
        context.State.Log($"{context.Source.Name} — {context.Controller.Name} gains damage prevention from {color} sources.");
        return Task.CompletedTask;
    }
}
