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
                await context.State.PerformDiscardAsync(card, player, context.Controller.Id, ct);
            }
        }
    }
}
