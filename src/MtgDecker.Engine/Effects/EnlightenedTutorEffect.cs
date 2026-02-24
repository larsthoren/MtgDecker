using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Effects;

/// <summary>
/// Enlightened Tutor - Search your library for an artifact or enchantment card,
/// reveal it, then shuffle your library and put that card on top of it.
/// </summary>
public class EnlightenedTutorEffect : SpellEffect
{
    public override async Task ResolveAsync(GameState state, StackObject spell,
        IPlayerDecisionHandler handler, CancellationToken ct = default)
    {
        var controller = state.GetPlayer(spell.ControllerId);

        var matches = controller.Library.Cards
            .Where(c => c.CardTypes.HasFlag(CardType.Artifact) || c.CardTypes.HasFlag(CardType.Enchantment))
            .ToList();

        if (matches.Count == 0)
        {
            state.Log($"{controller.Name} searches library but finds no artifact or enchantment.");
            controller.Library.Shuffle();
            return;
        }

        var chosenId = await handler.ChooseCard(
            matches, "Search for an artifact or enchantment", optional: true, ct);

        if (chosenId.HasValue)
        {
            var chosen = controller.Library.RemoveById(chosenId.Value);
            if (chosen != null)
            {
                controller.Library.Shuffle();
                controller.Library.AddToTop(chosen);
                state.Log($"{controller.Name} searches library and puts {chosen.Name} on top (Enlightened Tutor).");
                return;
            }
        }
        else
        {
            state.Log($"{controller.Name} declines to search.");
        }

        controller.Library.Shuffle();
    }
}
