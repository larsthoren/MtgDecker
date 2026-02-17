namespace MtgDecker.Engine.Triggers.Effects;

public class GainLifeEffect(int amount) : IEffect
{
    public int Amount { get; } = amount;

    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        context.Controller.AdjustLife(Amount);
        context.State.Log($"{context.Controller.Name} gains {Amount} life. ({context.Controller.Life} life)");
        return Task.CompletedTask;
    }
}
