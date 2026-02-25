namespace MtgDecker.Engine.Triggers.Effects;

public class ActivePlayerDiscardsRandomEffect(int count = 1) : IEffect
{
    public async Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var activePlayer = context.State.ActivePlayer;

        for (int i = 0; i < count && activePlayer.Hand.Cards.Count > 0; i++)
        {
            var random = Random.Shared.Next(activePlayer.Hand.Cards.Count);
            var card = activePlayer.Hand.Cards[random];
            activePlayer.Hand.Remove(card);
            context.State.LastDiscardCausedByPlayerId = context.Controller.Id;
            if (context.State.HandleDiscardAsync != null)
                await context.State.HandleDiscardAsync(card, activePlayer, ct);
            else
            {
                activePlayer.Graveyard.Add(card);
                context.State.Log($"{activePlayer.Name} discards {card.Name} at random to {context.Source.Name}.");
            }
        }
    }
}
