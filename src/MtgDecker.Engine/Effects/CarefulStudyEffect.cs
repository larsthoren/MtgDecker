namespace MtgDecker.Engine.Effects;

/// <summary>
/// Careful Study - Draw two cards, then discard two cards.
/// </summary>
public class CarefulStudyEffect : SpellEffect
{
    public override async Task ResolveAsync(GameState state, StackObject spell,
        IPlayerDecisionHandler handler, CancellationToken ct = default)
    {
        var player = state.GetPlayer(spell.ControllerId);

        // Draw 2
        var drawn = 0;
        for (int i = 0; i < 2; i++)
        {
            var card = player.Library.DrawFromTop();
            if (card == null) break;
            player.Hand.Add(card);
            drawn++;
        }
        state.Log($"{player.Name} draws {drawn} card(s) (Careful Study).");

        // Discard 2
        var toDiscard = Math.Min(2, player.Hand.Count);
        for (int i = 0; i < toDiscard; i++)
        {
            var cardId = await handler.ChooseCard(
                player.Hand.Cards,
                $"Choose a card to discard ({i + 1}/{toDiscard})",
                optional: false, ct);
            if (cardId.HasValue)
            {
                var card = player.Hand.RemoveById(cardId.Value);
                if (card != null)
                {
                    state.LastDiscardCausedByPlayerId = spell.ControllerId;
                    if (state.HandleDiscardAsync != null)
                        await state.HandleDiscardAsync(card, player, ct);
                    else
                        player.Graveyard.Add(card);
                }
            }
        }
        state.Log($"{player.Name} discards {toDiscard} card(s) (Careful Study).");
    }
}
