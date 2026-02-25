namespace MtgDecker.Engine.Effects;

/// <summary>
/// Frantic Search - Draw two cards, then discard two cards. Untap up to three lands.
/// </summary>
public class FranticSearchEffect : SpellEffect
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
        state.Log($"{player.Name} draws {drawn} card(s) (Frantic Search).");

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
        state.Log($"{player.Name} discards {toDiscard} card(s) (Frantic Search).");

        // Untap up to 3 lands
        var tappedLands = player.Battlefield.Cards
            .Where(c => c.IsLand && c.IsTapped)
            .ToList();

        var untapped = 0;
        for (int i = 0; i < 3 && tappedLands.Count > 0; i++)
        {
            var cardId = await handler.ChooseCard(
                tappedLands,
                $"Choose a land to untap ({i + 1}/3)",
                optional: true, ct);
            if (cardId.HasValue)
            {
                var land = tappedLands.FirstOrDefault(c => c.Id == cardId.Value);
                if (land != null)
                {
                    land.IsTapped = false;
                    tappedLands.Remove(land);
                    untapped++;
                }
            }
            else
            {
                break; // Player declined to untap more
            }
        }
        if (untapped > 0)
            state.Log($"{player.Name} untaps {untapped} land(s) (Frantic Search).");
    }
}
