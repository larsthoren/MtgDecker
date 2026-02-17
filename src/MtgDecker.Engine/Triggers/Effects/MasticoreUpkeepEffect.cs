namespace MtgDecker.Engine.Triggers.Effects;

public class MasticoreUpkeepEffect : IEffect
{
    public async Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var player = context.Controller;
        var card = context.Source;

        // Card no longer on battlefield — fizzled
        if (!player.Battlefield.Contains(card.Id)) return;

        // If player has cards in hand, ask if they want to discard one
        if (player.Hand.Cards.Count > 0)
        {
            var handCards = player.Hand.Cards.ToList();
            var chosenId = await context.DecisionHandler.ChooseCard(
                handCards, $"Discard a card to keep {card.Name}?", optional: true, ct);

            if (chosenId.HasValue)
            {
                var discarded = player.Hand.RemoveById(chosenId.Value);
                if (discarded != null)
                {
                    player.Graveyard.Add(discarded);
                    context.State.Log($"{player.Name} discards {discarded.Name} to keep {card.Name}.");
                    return;
                }
            }
        }

        // Sacrifice Masticore
        if (context.FireLeaveBattlefieldTriggers != null)
            await context.FireLeaveBattlefieldTriggers(card);
        player.Battlefield.RemoveById(card.Id);
        player.Graveyard.Add(card);
        if (card.IsToken)
            player.Graveyard.RemoveById(card.Id);
        context.State.Log($"{card.Name} is sacrificed (upkeep cost not paid).");
    }
}
