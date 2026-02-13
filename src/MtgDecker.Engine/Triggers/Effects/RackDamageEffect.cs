namespace MtgDecker.Engine.Triggers.Effects;

public class RackDamageEffect : IEffect
{
    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var opponent = context.State.GetOpponent(context.Controller);
        int damage = Math.Max(0, 3 - opponent.Hand.Cards.Count);
        if (damage > 0)
        {
            opponent.AdjustLife(-damage);
            context.State.Log($"{context.Source.Name} deals {damage} damage to {opponent.Name}. ({opponent.Hand.Cards.Count} cards in hand)");
        }
        return Task.CompletedTask;
    }
}
