using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Triggers.Effects;

public class SearchLibraryToTopEffect(CardType type) : IEffect
{
    public async Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var matches = context.Controller.Library.Cards
            .Where(c => c.CardTypes.HasFlag(type))
            .ToList();

        if (matches.Count == 0)
        {
            context.State.Log($"{context.Controller.Name} searches library but finds no {type}.");
            context.Controller.Library.Shuffle();
            return;
        }

        var chosenId = await context.DecisionHandler.ChooseCard(
            matches, $"Search for a {type}", optional: true, ct);

        if (chosenId.HasValue)
        {
            var chosen = context.Controller.Library.RemoveById(chosenId.Value);
            if (chosen != null)
            {
                context.Controller.Library.Shuffle();
                context.Controller.Library.AddToTop(chosen);
                context.State.Log($"{context.Controller.Name} searches library and puts {chosen.Name} on top.");
                return;
            }
        }
        else
        {
            context.State.Log($"{context.Controller.Name} declines to search.");
        }

        context.Controller.Library.Shuffle();
    }
}
