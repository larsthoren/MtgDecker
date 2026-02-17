using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Triggers.Effects;

public class GoblinGuideRevealEffect : IEffect
{
    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var opponent = context.State.Player1.Id == context.Controller.Id
            ? context.State.Player2 : context.State.Player1;

        var topCards = opponent.Library.PeekTop(1);
        if (topCards.Count == 0)
        {
            context.State.Log($"{opponent.Name} has no cards in library (Goblin Guide).");
            return Task.CompletedTask;
        }

        var revealed = topCards[0];
        context.State.Log($"{opponent.Name} reveals {revealed.Name} (Goblin Guide).");

        if (revealed.CardTypes.HasFlag(CardType.Land))
        {
            var card = opponent.Library.DrawFromTop();
            if (card != null)
            {
                opponent.Hand.Add(card);
                context.State.Log($"{opponent.Name} puts {card.Name} into their hand (land).");
            }
        }

        return Task.CompletedTask;
    }
}
