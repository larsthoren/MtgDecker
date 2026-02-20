namespace MtgDecker.Engine.Effects;

public class PonderEffect : SpellEffect
{
    public override async Task ResolveAsync(GameState state, StackObject spell,
        IPlayerDecisionHandler handler, CancellationToken ct = default)
    {
        var player = state.GetPlayer(spell.ControllerId);
        var top3 = player.Library.PeekTop(3).ToList();

        if (top3.Count == 0)
        {
            state.Log($"{player.Name} has no cards to look at (Ponder).");
            var drawn = player.Library.DrawFromTop();
            if (drawn != null)
            {
                player.Hand.Add(drawn);
                state.Log($"{player.Name} draws a card.");
            }
            return;
        }

        // Remove the top cards from library so the player can reorder them
        foreach (var card in top3)
            player.Library.RemoveById(card.Id);

        // Player puts cards back one by one — each placed card goes on top,
        // so the last card placed ends up on top of the library.
        var remaining = new List<GameCard>(top3);
        int total = remaining.Count;
        int placed = 0;

        while (remaining.Count > 1)
        {
            placed++;
            var chosenId = await handler.ChooseCard(
                remaining,
                $"Ponder: Put a card on top of your library ({placed} of {total})",
                optional: false, ct);

            if (chosenId.HasValue)
            {
                var chosen = remaining.First(c => c.Id == chosenId.Value);
                remaining.Remove(chosen);
                player.Library.AddToTop(chosen);
            }
        }

        // Auto-place last card on top
        player.Library.AddToTop(remaining[0]);
        state.Log($"{player.Name} puts cards back in chosen order (Ponder).");

        // Shuffle prompt: show the top card, Skip = shuffle, Choose = keep order
        var topCard = player.Library.PeekTop(1).ToList();
        var shuffleDecision = await handler.ChooseCard(
            topCard,
            "You may shuffle your library. Choose to keep order, or Skip to shuffle.",
            optional: true, ct);

        if (!shuffleDecision.HasValue)
        {
            player.Library.Shuffle();
            state.Log($"{player.Name} shuffles their library (Ponder).");
        }
        else
        {
            state.Log($"{player.Name} keeps the card order (Ponder).");
        }

        // Draw 1
        var drawn2 = player.Library.DrawFromTop();
        if (drawn2 != null)
        {
            player.Hand.Add(drawn2);
            state.Log($"{player.Name} draws a card.");
        }
    }
}
