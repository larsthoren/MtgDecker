namespace MtgDecker.Engine.Triggers.Effects;

public class RearrangeTopEffect(int count) : IEffect
{
    public async Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var topCards = new List<GameCard>();
        for (int i = 0; i < count && context.Controller.Library.Count > 0; i++)
        {
            var card = context.Controller.Library.DrawFromTop();
            if (card != null) topCards.Add(card);
        }

        if (topCards.Count == 0) return;

        // Let the player order all cards (pick top first, then second, etc.)
        var ordered = new List<GameCard>();
        var remaining = new List<GameCard>(topCards);

        while (remaining.Count > 1)
        {
            var chosenId = await context.DecisionHandler.ChooseCard(
                remaining, $"Choose card to put on top (position {ordered.Count + 1})", optional: false, ct);

            var chosen = remaining.FirstOrDefault(c => c.Id == chosenId);
            if (chosen != null)
            {
                ordered.Add(chosen);
                remaining.Remove(chosen);
            }
            else
            {
                break;
            }
        }

        // Last card goes automatically
        if (remaining.Count == 1)
            ordered.Add(remaining[0]);

        // Put back in reverse order so first chosen ends up on top
        for (int i = ordered.Count - 1; i >= 0; i--)
            context.Controller.Library.AddToTop(ordered[i]);

        context.State.Log($"{context.Controller.Name} rearranges top {topCards.Count} cards.");
    }
}
