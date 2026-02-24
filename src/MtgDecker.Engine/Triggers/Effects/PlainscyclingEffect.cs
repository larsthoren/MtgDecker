using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Triggers.Effects;

public class PlainscyclingEffect(string landSubtype = "Plains") : IEffect
{
    public async Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var matches = context.Controller.Library.Cards
            .Where(c => c.Subtypes.Contains(landSubtype, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 0)
        {
            context.State.Log($"{context.Controller.Name} searches library but finds no {landSubtype}.");
            context.Controller.Library.Shuffle();
            return;
        }

        var chosenId = await context.DecisionHandler.ChooseCard(
            matches, $"Search for a {landSubtype}", optional: true, ct);

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
