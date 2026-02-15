namespace MtgDecker.Engine.Triggers.Effects;

public class ReturnExiledCardsEffect : IEffect
{
    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var exiledIds = context.Source.ExiledCardIds.ToList();
        if (exiledIds.Count == 0) return Task.CompletedTask;

        foreach (var cardId in exiledIds)
        {
            GameCard? card = null;
            Player? owner = null;

            card = context.State.Player1.Exile.RemoveById(cardId);
            if (card != null) owner = context.State.Player1;

            if (card == null)
            {
                card = context.State.Player2.Exile.RemoveById(cardId);
                if (card != null) owner = context.State.Player2;
            }

            if (card != null && owner != null)
            {
                owner.Battlefield.Add(card);
                if (card.EntersTapped) card.IsTapped = true;
                context.State.Log($"{card.Name} returns to the battlefield.");
            }
        }

        context.Source.ExiledCardIds.Clear();
        return Task.CompletedTask;
    }
}
