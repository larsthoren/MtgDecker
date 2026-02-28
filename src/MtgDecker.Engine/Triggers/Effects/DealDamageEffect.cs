namespace MtgDecker.Engine.Triggers.Effects;

public class DealDamageEffect(int amount) : IEffect
{
    public int Amount { get; } = amount;

    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        if (context.Target != null)
        {
            context.Target.DamageMarked += Amount;
            context.State.Log($"{context.Source.Name} deals {Amount} damage to {context.Target.Name}.");
        }
        else if (context.TargetPlayerId.HasValue)
        {
            var target = context.State.Player1.Id == context.TargetPlayerId.Value
                ? context.State.Player1
                : context.State.Player2;

            var hasDamageProtection = context.State.ActiveEffects.Any(e =>
                e.Type == ContinuousEffectType.PreventDamageToPlayer
                && context.State.GetCardController(e.SourceId)?.Id == target.Id);

            // Check for color-specific shield (Circle of Protection)
            var colorShield = target.DamagePreventionShields
                .FirstOrDefault(s => context.Source.Colors.Contains(s.Color));

            if (hasDamageProtection)
            {
                context.State.Log($"Damage to {target.Name} is prevented (protection).");
            }
            else if (colorShield != null)
            {
                target.DamagePreventionShields.Remove(colorShield);
                context.State.Log($"Damage to {target.Name} is prevented (Circle of Protection).");
            }
            else
            {
                target.AdjustLife(-Amount);
                context.State.Log($"{context.Source.Name} deals {Amount} damage to {target.Name}. ({target.Life} life)");
            }
        }

        return Task.CompletedTask;
    }
}
