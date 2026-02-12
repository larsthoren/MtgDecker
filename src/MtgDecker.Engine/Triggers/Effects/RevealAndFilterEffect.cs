namespace MtgDecker.Engine.Triggers.Effects;

public class RevealAndFilterEffect(int count, string subtype) : IEffect
{
    public async Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var revealed = context.Controller.Library.PeekTop(count);
        if (revealed.Count == 0)
        {
            context.State.Log($"{context.Controller.Name} reveals top {count} — library is empty.");
            return;
        }

        // Remove all revealed from library
        foreach (var card in revealed)
            context.Controller.Library.RemoveById(card.Id);

        var matching = revealed.Where(c =>
            c.Subtypes.Contains(subtype, StringComparer.OrdinalIgnoreCase)).ToList();
        var nonMatching = revealed.Where(c =>
            !c.Subtypes.Contains(subtype, StringComparer.OrdinalIgnoreCase)).ToList();

        // Show revealed cards to player
        await context.DecisionHandler.RevealCards(
            revealed.ToList(), matching,
            $"Revealed {revealed.Count} cards — {matching.Count} {subtype}(s) found", ct);

        // Matching → hand
        foreach (var card in matching)
        {
            context.Controller.Hand.Add(card);
            context.State.Log($"{context.Controller.Name} puts {card.Name} into hand.");
        }

        // Non-matching → bottom of library
        foreach (var card in nonMatching)
            context.Controller.Library.AddToBottom(card);

        if (nonMatching.Count > 0)
            context.State.Log($"{context.Controller.Name} puts {nonMatching.Count} card(s) on the bottom of library.");
    }
}
