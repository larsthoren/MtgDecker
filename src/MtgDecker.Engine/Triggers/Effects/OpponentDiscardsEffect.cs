namespace MtgDecker.Engine.Triggers.Effects;

public class OpponentDiscardsEffect : IEffect
{
    public int Count { get; }
    public OpponentDiscardsEffect(int count = 1) => Count = count;

    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var opponent = context.State.GetOpponent(context.Controller);
        for (int i = 0; i < Count && opponent.Hand.Cards.Count > 0; i++)
        {
            var card = opponent.Hand.Cards[^1];
            opponent.Hand.Remove(card);
            opponent.Graveyard.Add(card);
            context.State.Log($"{opponent.Name} discards {card.Name}.");
        }
        return Task.CompletedTask;
    }
}
