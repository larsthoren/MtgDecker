namespace MtgDecker.Engine.Triggers.Effects;

public class SacrificeSpecificCardEffect(Guid cardId) : IEffect
{
    public Guid CardId { get; } = cardId;

    public async Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var card = context.Controller.Battlefield.Cards.FirstOrDefault(c => c.Id == CardId);
        if (card == null) return;

        if (context.FireLeaveBattlefieldTriggers != null)
            await context.FireLeaveBattlefieldTriggers(card);

        context.Controller.Battlefield.RemoveById(card.Id);
        context.Controller.Graveyard.Add(card);
        context.State.Log($"{context.Controller.Name} sacrifices {card.Name} (end of turn).");
    }
}
