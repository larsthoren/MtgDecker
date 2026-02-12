namespace MtgDecker.Engine.Triggers.Effects;

public class SearchLibraryEffect(string subtype, bool optional = true) : IEffect
{
    public async Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var matches = context.Controller.Library.Cards
            .Where(c => c.Subtypes.Contains(subtype, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 0)
        {
            context.State.Log($"{context.Controller.Name} searches library but finds no {subtype}.");
            context.Controller.Library.Shuffle();
            return;
        }

        var chosenId = await context.DecisionHandler.ChooseCard(
            matches, $"Search for a {subtype}", optional, ct);

        if (chosenId.HasValue)
        {
            var chosen = context.Controller.Library.RemoveById(chosenId.Value);
            if (chosen != null)
            {
                context.Controller.Hand.Add(chosen);
                context.State.Log($"{context.Controller.Name} searches library and adds {chosen.Name} to hand.");
            }
        }
        else
        {
            context.State.Log($"{context.Controller.Name} declines to search.");
        }

        context.Controller.Library.Shuffle();
    }
}
