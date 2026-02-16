namespace MtgDecker.Engine.Triggers.Effects;

public class ExileFromOpponentGraveyardEffect : IEffect
{
    public async Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var opponent = context.State.Player1.Id == context.Controller.Id
            ? context.State.Player2 : context.State.Player1;

        if (opponent.Graveyard.Count == 0)
        {
            context.State.Log($"{opponent.Name}'s graveyard is empty.");
            return;
        }

        var chosenId = await context.DecisionHandler.ChooseCard(
            opponent.Graveyard.Cards,
            "Choose a card from opponent's graveyard to exile",
            optional: false, ct);

        if (chosenId.HasValue)
        {
            var card = opponent.Graveyard.RemoveById(chosenId.Value);
            if (card != null)
            {
                opponent.Exile.Add(card);
                context.State.Log($"{card.Name} is exiled from {opponent.Name}'s graveyard.");
            }
        }
    }
}
