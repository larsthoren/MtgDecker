using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Triggers.Effects;

public class ReturnSpellToTopEffect : IEffect
{
    public async Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var spells = context.Controller.Graveyard.Cards
            .Where(c => c.CardTypes.HasFlag(CardType.Instant) || c.CardTypes.HasFlag(CardType.Sorcery))
            .ToList();

        if (spells.Count == 0)
        {
            context.State.Log($"{context.Controller.Name} has no instant or sorcery in graveyard.");
            return;
        }

        var chosenId = await context.DecisionHandler.ChooseCard(
            spells, "Choose an instant or sorcery to put on top of your library", optional: true, ct);

        if (chosenId.HasValue)
        {
            var card = context.Controller.Graveyard.RemoveById(chosenId.Value);
            if (card != null)
            {
                context.Controller.Library.AddToTop(card);
                context.State.Log($"{context.Controller.Name} puts {card.Name} on top of library.");
            }
        }
    }
}
