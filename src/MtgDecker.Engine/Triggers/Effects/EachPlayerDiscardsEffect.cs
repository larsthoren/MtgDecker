namespace MtgDecker.Engine.Triggers.Effects;

public class EachPlayerDiscardsEffect : IEffect
{
    public int Count { get; }
    public EachPlayerDiscardsEffect(int count = 1) => Count = count;

    public async Task Execute(EffectContext context, CancellationToken ct = default)
    {
        foreach (var player in context.State.Players)
        {
            for (int i = 0; i < Count && player.Hand.Cards.Count > 0; i++)
            {
                var card = player.Hand.Cards[^1];
                player.Hand.Remove(card);
                context.State.LastDiscardCausedByPlayerId = context.Controller.Id;
                if (context.State.HandleDiscardAsync != null)
                    await context.State.HandleDiscardAsync(card, player, ct);
                else
                {
                    player.Graveyard.Add(card);
                    context.State.Log($"{player.Name} discards {card.Name} to {context.Source.Name}.");
                }
            }
        }
    }
}
