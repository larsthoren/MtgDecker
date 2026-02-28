namespace MtgDecker.Engine.Triggers.Effects;

public enum SearchDestination { Hand, Battlefield, TopOfLibrary }

public class SearchLibraryEffect(
    Func<GameCard, bool> filter,
    string description,
    SearchDestination destination = SearchDestination.Hand,
    bool optional = true) : IEffect
{
    // Convenience constructor for subtype search (preserves old API for "Goblin" etc.)
    public SearchLibraryEffect(string subtype, bool optional = true)
        : this(c => c.Subtypes.Contains(subtype, StringComparer.OrdinalIgnoreCase),
               subtype, SearchDestination.Hand, optional) { }

    public async Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var matches = context.Controller.Library.Cards
            .Where(filter)
            .ToList();

        if (matches.Count == 0)
        {
            context.State.Log($"{context.Controller.Name} searches library but finds no {description}.");
            context.Controller.Library.Shuffle();
            return;
        }

        var chosenId = await context.DecisionHandler.ChooseCard(
            matches, $"Search for a {description}", optional, ct);

        if (chosenId.HasValue)
        {
            var chosen = context.Controller.Library.RemoveById(chosenId.Value);
            if (chosen != null)
            {
                switch (destination)
                {
                    case SearchDestination.Hand:
                        context.Controller.Hand.Add(chosen);
                        context.State.Log($"{context.Controller.Name} searches library and adds {chosen.Name} to hand.");
                        break;
                    case SearchDestination.Battlefield:
                        chosen.TurnEnteredBattlefield = context.State.TurnNumber;
                        context.Controller.Battlefield.Add(chosen);
                        context.State.Log($"{context.Controller.Name} puts {chosen.Name} onto the battlefield.");
                        break;
                    case SearchDestination.TopOfLibrary:
                        context.Controller.Library.Shuffle();
                        context.Controller.Library.AddToTop(chosen);
                        context.State.Log($"{context.Controller.Name} searches library and puts {chosen.Name} on top.");
                        return; // Already shuffled before placing on top
                }
            }
        }
        else
        {
            context.State.Log($"{context.Controller.Name} declines to search.");
        }

        context.Controller.Library.Shuffle();
    }
}
