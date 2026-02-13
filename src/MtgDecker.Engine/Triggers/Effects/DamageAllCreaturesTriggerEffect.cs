namespace MtgDecker.Engine.Triggers.Effects;

public class DamageAllCreaturesTriggerEffect : IEffect
{
    public int Amount { get; }
    public bool IncludePlayers { get; }

    public DamageAllCreaturesTriggerEffect(int amount, bool includePlayers = false)
    {
        Amount = amount;
        IncludePlayers = includePlayers;
    }

    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        foreach (var player in new[] { context.State.Player1, context.State.Player2 })
        {
            foreach (var creature in player.Battlefield.Cards.Where(c => c.IsCreature))
                creature.DamageMarked += Amount;
            if (IncludePlayers)
                player.AdjustLife(-Amount);
        }
        context.State.Log($"{context.Source.Name} deals {Amount} damage to all creatures{(IncludePlayers ? " and players" : "")}.");
        return Task.CompletedTask;
    }
}
