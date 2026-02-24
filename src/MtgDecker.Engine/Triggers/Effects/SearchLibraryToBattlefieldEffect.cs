namespace MtgDecker.Engine.Triggers.Effects;

public class SearchLibraryToBattlefieldEffect(string subtype, int maxCmc, bool optional = true) : IEffect
{
    public async Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var matches = context.Controller.Library.Cards
            .Where(c => c.Subtypes.Contains(subtype, StringComparer.OrdinalIgnoreCase)
                && (c.ManaCost?.ConvertedManaCost ?? 0) <= maxCmc)
            .ToList();

        if (matches.Count == 0)
        {
            context.State.Log($"{context.Controller.Name} searches library but finds no {subtype} with CMC {maxCmc} or less.");
            context.Controller.Library.Shuffle();
            return;
        }

        var chosenId = await context.DecisionHandler.ChooseCard(
            matches, $"Search for a {subtype} (CMC {maxCmc} or less)", optional, ct);

        if (chosenId.HasValue)
        {
            var chosen = context.Controller.Library.RemoveById(chosenId.Value);
            if (chosen != null)
            {
                context.Controller.Battlefield.Add(chosen);
                chosen.TurnEnteredBattlefield = context.State.TurnNumber;
                context.State.Log($"{context.Controller.Name} puts {chosen.Name} onto the battlefield.");
            }
        }
        else
        {
            context.State.Log($"{context.Controller.Name} declines to search.");
        }

        context.Controller.Library.Shuffle();
    }
}
