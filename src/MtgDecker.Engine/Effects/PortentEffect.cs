using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Effects;

/// <summary>
/// Portent - Look at the top three cards of target player's library, then put them back
/// in any order. You may have that player shuffle. Draw a card at the beginning of the
/// next turn's upkeep.
/// </summary>
public class PortentEffect : SpellEffect
{
    public override async Task ResolveAsync(GameState state, StackObject spell,
        IPlayerDecisionHandler handler, CancellationToken ct = default)
    {
        var controller = state.GetPlayer(spell.ControllerId);

        // Determine target player (defaults to controller if no target)
        var targetPlayer = spell.Targets.Count > 0
            ? state.GetPlayer(spell.Targets[0].PlayerId)
            : controller;

        // Look at top 3 of target player's library
        var top3 = new List<GameCard>();
        for (int i = 0; i < 3 && targetPlayer.Library.Count > 0; i++)
        {
            var card = targetPlayer.Library.DrawFromTop();
            if (card != null) top3.Add(card);
        }

        if (top3.Count > 0)
        {
            // Reorder cards (controller decides)
            var (ordered, shuffle) = await handler.ReorderCards(
                top3, $"Portent: Rearrange top {top3.Count} cards of {targetPlayer.Name}'s library", ct);

            // Place cards back: each AddToTop pushes previous down,
            // so first in ordered ends up deepest, last ends up on top.
            foreach (var card in ordered)
                targetPlayer.Library.AddToTop(card);

            state.Log($"{controller.Name} rearranges top {top3.Count} cards of {targetPlayer.Name}'s library (Portent).");

            if (shuffle)
            {
                targetPlayer.Library.Shuffle();
                state.Log($"{targetPlayer.Name} shuffles their library (Portent).");
            }
        }
        else
        {
            state.Log($"{targetPlayer.Name}'s library is empty (Portent).");
        }

        // Register a delayed trigger: draw a card at the beginning of the next turn's upkeep
        state.DelayedTriggers.Add(new DelayedTrigger(
            GameEvent.Upkeep,
            new DrawCardEffect(),
            controller.Id));
        state.Log($"{controller.Name} will draw a card at the beginning of the next turn's upkeep (Portent).");
    }
}
