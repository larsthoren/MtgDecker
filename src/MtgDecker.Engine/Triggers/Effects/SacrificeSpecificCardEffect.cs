namespace MtgDecker.Engine.Triggers.Effects;

public class SacrificeSpecificCardEffect(Guid cardId) : IEffect
{
    public Guid CardId { get; } = cardId;

    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var card = context.Controller.Battlefield.Cards.FirstOrDefault(c => c.Id == CardId);
        if (card == null) return Task.CompletedTask;

        context.Controller.Battlefield.RemoveById(card.Id);
        context.Controller.Graveyard.Add(card);
        context.State.Log($"{context.Controller.Name} sacrifices {card.Name} (end of turn).");
        return Task.CompletedTask;
    }
}
