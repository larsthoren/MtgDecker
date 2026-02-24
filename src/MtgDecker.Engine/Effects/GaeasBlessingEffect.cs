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

        // Target player is the controller (self-target for simplicity)
        // Choose up to 3 cards from graveyard to shuffle into library
        var graveyardCards = controller.Graveyard.Cards.ToList();
        if (graveyardCards.Count > 0)
        {
            var maxChoose = Math.Min(3, graveyardCards.Count);
            var shuffled = 0;

            for (int i = 0; i < maxChoose; i++)
            {
                var eligible = controller.Graveyard.Cards.ToList();
                if (eligible.Count == 0) break;

                var chosenId = await handler.ChooseCard(
                    eligible, $"Choose a card to shuffle into library ({i + 1}/{maxChoose})", optional: true, ct);

                if (!chosenId.HasValue) break; // Optional — player can stop choosing

                var card = controller.Graveyard.Cards.FirstOrDefault(c => c.Id == chosenId.Value);
                if (card != null)
                {
                    controller.Graveyard.RemoveById(card.Id);
                    controller.Library.Add(card);
                    shuffled++;
                }
            }

            if (shuffled > 0)
            {
                controller.Library.Shuffle();
                state.Log($"{controller.Name} shuffles {shuffled} card(s) from graveyard into library (Gaea's Blessing).");
            }
        }

        // Draw a card
        var drawn = controller.Library.DrawFromTop();
        if (drawn != null)
        {
            controller.Hand.Add(drawn);
            state.Log($"{controller.Name} draws a card (Gaea's Blessing).");
        }
    }
}
