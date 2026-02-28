namespace MtgDecker.Engine.Triggers.Effects;

public class DestroyAllSubtypeEffect(string subtype) : IEffect
{
    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        foreach (var player in context.State.Players)
        {
            var toDestroy = player.Battlefield.Cards
                .Where(c => c.Subtypes.Contains(subtype, StringComparer.OrdinalIgnoreCase))
                .ToList();

            foreach (var card in toDestroy)
            {
                player.Battlefield.RemoveById(card.Id);
                if (!card.IsToken)
                    player.Graveyard.Add(card);
                context.State.Log($"{card.Name} is destroyed.");
            }
        }
        return Task.CompletedTask;
    }
}
