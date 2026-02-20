namespace MtgDecker.Engine.Triggers.Effects;

public class SacrificeSelfOnLandEffect : IEffect
{
    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var card = context.Controller.Battlefield.Cards.FirstOrDefault(c => c.Id == context.Source.Id);
        if (card == null) return Task.CompletedTask;

        context.Controller.Battlefield.RemoveById(card.Id);
        context.Controller.Graveyard.Add(card);
        context.State.Log($"{context.Controller.Name} sacrifices {card.Name} (another land was played).");
        return Task.CompletedTask;
    }
}
