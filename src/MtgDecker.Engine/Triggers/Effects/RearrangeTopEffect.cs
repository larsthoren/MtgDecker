namespace MtgDecker.Engine.Triggers.Effects;

public class RearrangeTopEffect(int count) : IEffect
{
    public async Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var topCards = context.Controller.Library.PeekTop(count).ToList();

        if (topCards.Count == 0) return;

        // Remove the top cards from library so the player can reorder them
        foreach (var card in topCards)
            context.Controller.Library.RemoveById(card.Id);

        // Use the same ReorderCards UI as Ponder — player clicks cards to place back.
        // Returned ordered: first = placed first = deepest, last = placed last = top.
        var (ordered, _) = await context.DecisionHandler.ReorderCards(
            topCards, "Rearrange top cards", ct);

        // Place cards back: each AddToTop pushes previous down,
        // so first in ordered ends up deepest, last ends up on top.
        foreach (var card in ordered)
            context.Controller.Library.AddToTop(card);

        context.State.Log($"{context.Controller.Name} rearranges top {topCards.Count} cards.");
    }
}
