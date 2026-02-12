namespace MtgDecker.Engine.Triggers.Effects;

public class UpkeepCostEffect : IEffect
{
    public async Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var controller = context.Controller;
        var source = context.Source;

        if (controller.Hand.Count > 0)
        {
            var chosenId = await context.DecisionHandler.ChooseCard(
                controller.Hand.Cards.ToList(), $"Discard a card to keep {source.Name}", optional: true, ct);

            if (chosenId.HasValue)
            {
                var card = controller.Hand.RemoveById(chosenId.Value);
                if (card != null)
                {
                    controller.Graveyard.Add(card);
                    context.State.Log($"{controller.Name} discards {card.Name} to keep {source.Name}.");
                    return;
                }
            }
        }

        // No cards in hand or player declined — sacrifice the source
        controller.Battlefield.RemoveById(source.Id);
        controller.Graveyard.Add(source);
        context.State.Log($"{source.Name} is sacrificed (no discard).");
    }
}
