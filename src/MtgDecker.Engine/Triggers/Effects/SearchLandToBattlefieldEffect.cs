namespace MtgDecker.Engine.Triggers.Effects;

/// <summary>
/// Searches library for a land matching a filter and puts it onto the battlefield (optionally tapped).
/// Used by Yavimaya Granger (any basic land, enters tapped).
/// </summary>
public class SearchLandToBattlefieldEffect(Func<GameCard, bool> filter, bool entersTapped = false) : IEffect
{
    public async Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var matches = context.Controller.Library.Cards
            .Where(filter)
            .ToList();

        if (matches.Count == 0)
        {
            context.State.Log($"{context.Controller.Name} searches library but finds no matching land.");
            context.Controller.Library.Shuffle();
            return;
        }

        var chosenId = await context.DecisionHandler.ChooseCard(
            matches, "Search for a basic land", optional: true, ct);

        if (chosenId.HasValue)
        {
            var chosen = context.Controller.Library.RemoveById(chosenId.Value);
            if (chosen != null)
            {
                chosen.TurnEnteredBattlefield = context.State.TurnNumber;
                if (entersTapped) chosen.IsTapped = true;
                context.Controller.Battlefield.Add(chosen);
                context.State.Log($"{context.Controller.Name} puts {chosen.Name} onto the battlefield{(entersTapped ? " tapped" : "")}.");
            }
        }
        else
        {
            context.State.Log($"{context.Controller.Name} declines to search.");
        }

        context.Controller.Library.Shuffle();
    }
}
