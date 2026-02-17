namespace MtgDecker.Engine.Triggers.Effects;

public class ReturnSelfFromGraveyardEffect : IEffect
{
    public async Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var card = context.Controller.Graveyard.Cards
            .FirstOrDefault(c => c.Id == context.Source.Id);
        if (card == null) return;

        var choice = await context.DecisionHandler.ChooseCard(
            [card], $"Return {card.Name} from graveyard to hand?",
            optional: true, ct);

        if (choice.HasValue)
        {
            context.Controller.Graveyard.RemoveById(card.Id);
            context.Controller.Hand.Add(card);
            context.State.Log($"{context.Controller.Name} returns {card.Name} from graveyard to hand.");
        }
    }
}
