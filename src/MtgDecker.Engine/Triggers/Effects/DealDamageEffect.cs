namespace MtgDecker.Engine.Triggers.Effects;

public class DealDamageEffect(int amount) : IEffect
{
    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        if (context.Target != null)
        {
            context.Target.DamageMarked += amount;
            context.State.Log($"{context.Source.Name} deals {amount} damage to {context.Target.Name}.");
        }
        else if (context.TargetPlayerId.HasValue)
        {
            var target = context.State.Player1.Id == context.TargetPlayerId.Value
                ? context.State.Player1
                : context.State.Player2;
            target.AdjustLife(-amount);
            context.State.Log($"{context.Source.Name} deals {amount} damage to {target.Name}. ({target.Life} life)");
        }

        return Task.CompletedTask;
    }
}
