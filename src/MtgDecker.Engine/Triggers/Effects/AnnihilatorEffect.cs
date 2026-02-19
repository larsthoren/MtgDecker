namespace MtgDecker.Engine.Triggers.Effects;

public class AnnihilatorEffect(int count) : IEffect
{
    public async Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var defender = context.State.GetOpponent(context.Controller);
        var sacrificed = 0;

        context.State.Log($"Annihilator {count} — {defender.Name} must sacrifice {count} permanent(s).");

        while (sacrificed < count && defender.Battlefield.Cards.Count > 0)
        {
            var eligible = defender.Battlefield.Cards.ToList();
            var chosenId = await defender.DecisionHandler.ChooseCard(
                eligible, $"Sacrifice a permanent (annihilator — {count - sacrificed} remaining)",
                optional: false, ct);

            if (!chosenId.HasValue) break;

            var card = defender.Battlefield.Cards.FirstOrDefault(c => c.Id == chosenId.Value);
            if (card == null) break;

            if (context.FireLeaveBattlefieldTriggers != null)
                await context.FireLeaveBattlefieldTriggers(card);

            defender.Battlefield.RemoveById(card.Id);
            defender.Graveyard.Add(card);
            context.State.Log($"{defender.Name} sacrifices {card.Name}.");
            sacrificed++;
        }
    }
}
