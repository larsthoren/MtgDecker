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

        var chosenId = await context.DecisionHandler.ChooseCard(
            topCards, "Choose a card to put on top", optional: false, ct);

        var chosen = topCards.FirstOrDefault(c => c.Id == chosenId);
        var rest = topCards.Where(c => c.Id != chosenId).ToList();

        foreach (var card in rest)
            context.Controller.Library.AddToTop(card);
        if (chosen != null)
            context.Controller.Library.AddToTop(chosen);

        context.State.Log($"{context.Controller.Name} rearranges top {topCards.Count} cards.");
    }
}
