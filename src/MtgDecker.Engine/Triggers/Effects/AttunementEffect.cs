namespace MtgDecker.Engine.Triggers.Effects;

/// <summary>
/// Attunement activated ability effect - Draw three cards, then discard four cards.
/// </summary>
public class AttunementEffect : IEffect
{
    public async Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var player = context.Controller;
        var state = context.State;

        // Draw 3 cards
        int drawn = 0;
        for (int i = 0; i < 3; i++)
        {
            var card = player.Library.DrawFromTop();
            if (card != null)
            {
                player.Hand.Add(card);
                drawn++;
            }
            else
            {
                state.IsGameOver = true;
                state.Winner = state.GetOpponent(player).Name;
                state.Log($"{player.Name} cannot draw — loses the game.");
                return;
            }
        }
        state.Log($"{player.Name} draws {drawn} cards (Attunement).");

        // Discard 4 cards
        int toDiscard = Math.Min(4, player.Hand.Cards.Count);
        if (toDiscard == 0) return;

        var discarded = await context.DecisionHandler.ChooseCardsToDiscard(
            player.Hand.Cards.ToList(), toDiscard, ct);

        foreach (var card in discarded)
        {
            player.Hand.RemoveById(card.Id);
            if (state.HandleDiscardAsync != null)
                await state.HandleDiscardAsync(card, player, ct);
            else
            {
                player.Graveyard.Add(card);
                state.Log($"{player.Name} discards {card.Name} (Attunement).");
            }
        }
    }
}
