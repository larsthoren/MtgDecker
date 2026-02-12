namespace MtgDecker.Engine.Triggers.Effects;

public class SylvanLibraryEffect : IEffect
{
    public async Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var drawn = new List<GameCard>();
        for (int i = 0; i < 2; i++)
        {
            var card = context.Controller.Library.DrawFromTop();
            if (card != null)
            {
                context.Controller.Hand.Add(card);
                drawn.Add(card);
            }
        }

        if (drawn.Count == 0) return;

        context.State.Log($"{context.Controller.Name} draws {drawn.Count} extra cards (Sylvan Library).");

        for (int i = 0; i < drawn.Count; i++)
        {
            var remaining = drawn.Where(c => context.Controller.Hand.Contains(c.Id)).ToList();
            if (remaining.Count == 0) break;

            var chosenId = await context.DecisionHandler.ChooseCard(
                remaining, "Choose a card to put back on library (or decline to pay 4 life)",
                optional: true, ct);

            if (chosenId.HasValue)
            {
                var card = context.Controller.Hand.RemoveById(chosenId.Value);
                if (card != null)
                {
                    context.Controller.Library.AddToTop(card);
                    context.State.Log($"{context.Controller.Name} puts a card back on top of library.");
                }
            }
            else
            {
                // Player keeps all remaining drawn cards, paying 4 life for each
                foreach (var kept in remaining)
                {
                    if (context.Controller.Hand.Contains(kept.Id))
                    {
                        context.Controller.AdjustLife(-4);
                        context.State.Log($"{context.Controller.Name} pays 4 life to keep {kept.Name}. ({context.Controller.Life} life)");
                    }
                }
                break;
            }
        }
    }
}
