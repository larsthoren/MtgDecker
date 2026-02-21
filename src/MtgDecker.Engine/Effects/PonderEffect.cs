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

        // Player clicks cards one by one to place back.
        // Returned ordered: first = placed first = deepest, last = placed last = top.
        var (ordered, shuffle) = await handler.ReorderCards(
            top3, "Click a card to put on top", ct);

        // Place cards back: each AddToTop pushes previous down,
        // so first in ordered ends up deepest, last ends up on top.
        foreach (var card in ordered)
            player.Library.AddToTop(card);

        state.Log($"{player.Name} puts cards back in chosen order (Ponder).");

        if (shuffle)
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
