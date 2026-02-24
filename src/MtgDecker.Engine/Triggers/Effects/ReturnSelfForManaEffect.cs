using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine.Triggers.Effects;

public class ReturnSelfForManaEffect(ManaCost cost) : IEffect
{
    public ManaCost Cost { get; } = cost;

    public async Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var card = context.Controller.Graveyard.Cards
            .FirstOrDefault(c => c.Id == context.Source.Id);
        if (card == null) return;

        if (!context.Controller.ManaPool.CanPay(Cost))
        {
            context.State.Log($"{context.Controller.Name} cannot pay {Cost} to return {card.Name}.");
            return;
        }

        var choice = await context.DecisionHandler.ChooseCard(
            [card], $"Pay {Cost} to return {card.Name} from graveyard to hand?",
            optional: true, ct);

        if (choice.HasValue)
        {
            context.Controller.ManaPool.Pay(Cost);
            context.Controller.Graveyard.RemoveById(card.Id);
            context.Controller.Hand.Add(card);
            context.State.Log($"{context.Controller.Name} pays {Cost} and returns {card.Name} to hand.");
        }
    }
}
