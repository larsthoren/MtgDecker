namespace MtgDecker.Engine.Triggers.Effects;

/// <summary>
/// Phyrexian Furnace activated ability — {1}, Sacrifice Phyrexian Furnace:
/// Exile target card from a graveyard. Draw a card.
/// </summary>
public class PhyrexianFurnaceEffect : IEffect
{
    public async Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var allGraveyardCards = context.State.Player1.Graveyard.Cards
            .Concat(context.State.Player2.Graveyard.Cards)
            .ToList();

        if (allGraveyardCards.Count == 0)
        {
            context.State.Log("No cards in any graveyard for Phyrexian Furnace.");
        }
        else
        {
            var chosenId = await context.DecisionHandler.ChooseCard(
                allGraveyardCards, "Choose a card from any graveyard to exile", optional: false, ct);

            if (chosenId.HasValue)
            {
                var card = context.State.Player1.Graveyard.RemoveById(chosenId.Value)
                        ?? context.State.Player2.Graveyard.RemoveById(chosenId.Value);
                if (card != null)
                {
                    context.Controller.Exile.Add(card);
                    context.State.Log($"{card.Name} is exiled from graveyard.");
                }
            }
        }

        // Draw a card
        var drawn = context.Controller.Library.DrawFromTop();
        if (drawn != null)
        {
            context.Controller.Hand.Add(drawn);
            context.State.Log($"{context.Controller.Name} draws a card.");
        }
    }
}
