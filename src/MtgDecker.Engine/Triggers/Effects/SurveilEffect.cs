namespace MtgDecker.Engine.Triggers.Effects;

public class SurveilEffect(int amount) : IEffect
{
    public int Amount { get; } = amount;

    public async Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var topCards = context.Controller.Library.PeekTop(Amount);
        if (topCards.Count == 0) return;

        context.State.Log($"{context.Controller.Name} surveils {topCards.Count}.");

        foreach (var card in topCards)
        {
            var choice = await context.DecisionHandler.ChooseCard(
                [card],
                $"Surveil: Put {card.Name} into graveyard?",
                optional: true, ct);

            if (choice.HasValue)
            {
                context.Controller.Library.Remove(card);
                context.Controller.Graveyard.Add(card);
                context.State.Log($"{context.Controller.Name} puts {card.Name} into graveyard (surveil).");
            }
        }
    }
}
