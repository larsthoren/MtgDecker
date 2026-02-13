namespace MtgDecker.Engine.Triggers.Effects;

public class DamageAllPlayersTriggerEffect : IEffect
{
    public int Amount { get; }
    public DamageAllPlayersTriggerEffect(int amount) => Amount = amount;

    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        context.State.Player1.AdjustLife(-Amount);
        context.State.Player2.AdjustLife(-Amount);
        context.State.Log($"{context.Source.Name} deals {Amount} damage to each player.");
        return Task.CompletedTask;
    }
}
