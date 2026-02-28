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
            await context.State.PerformDiscardAsync(card, activePlayer, context.Controller.Id, ct);
        }
    }
}
