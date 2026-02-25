namespace MtgDecker.Engine.Effects;

/// <summary>
/// Gaea's Blessing - Target player shuffles up to three target cards from their graveyard
/// into their library. Draw a card.
/// </summary>
public class GaeasBlessingEffect : SpellEffect
{
    public override async Task ResolveAsync(GameState state, StackObject spell,
        IPlayerDecisionHandler handler, CancellationToken ct = default)
    {
        var controller = state.GetPlayer(spell.ControllerId);

        // Target player (from spell targets)
        Player targetPlayer;
        if (spell.Targets.Count > 0)
            targetPlayer = state.GetPlayer(spell.Targets[0].PlayerId);
        else
            targetPlayer = controller;

        // Choose up to 3 cards from target player's graveyard to shuffle into their library
        var graveyardCards = targetPlayer.Graveyard.Cards.ToList();
        if (graveyardCards.Count > 0)
        {
            var maxChoose = Math.Min(3, graveyardCards.Count);
            var shuffled = 0;

            for (int i = 0; i < maxChoose; i++)
            {
                var eligible = targetPlayer.Graveyard.Cards.ToList();
                if (eligible.Count == 0) break;

                var chosenId = await handler.ChooseCard(
                    eligible, $"Choose a card to shuffle into library ({i + 1}/{maxChoose})", optional: true, ct);

                if (!chosenId.HasValue) break; // Optional — player can stop choosing

                var card = targetPlayer.Graveyard.Cards.FirstOrDefault(c => c.Id == chosenId.Value);
                if (card != null)
                {
                    targetPlayer.Graveyard.RemoveById(card.Id);
                    targetPlayer.Library.Add(card);
                    shuffled++;
                }
            }

            if (shuffled > 0)
            {
                targetPlayer.Library.Shuffle();
                state.Log($"{targetPlayer.Name} shuffles {shuffled} card(s) from graveyard into library (Gaea's Blessing).");
            }
        }

        // Caster draws a card (always the controller, not the target)
        var drawn = controller.Library.DrawFromTop();
        if (drawn != null)
        {
            controller.Hand.Add(drawn);
            state.Log($"{controller.Name} draws a card (Gaea's Blessing).");
        }
    }
}
