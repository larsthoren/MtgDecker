namespace MtgDecker.Engine.Triggers.Effects;

/// <summary>
/// Deals a fixed amount of damage to a specific player (by ID).
/// Used for delayed triggers like Searing Blood.
/// </summary>
public class DealDamageToPlayerEffect(int amount, Guid targetPlayerId) : IEffect
{
    public int Amount { get; } = amount;
    public Guid TargetPlayerId { get; } = targetPlayerId;

    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var target = context.State.Player1.Id == TargetPlayerId
            ? context.State.Player1
            : context.State.Player2;

        target.AdjustLife(-Amount);
        context.State.Log($"Searing Blood deals {Amount} damage to {target.Name}. ({target.Life} life)");

        return Task.CompletedTask;
    }
}
