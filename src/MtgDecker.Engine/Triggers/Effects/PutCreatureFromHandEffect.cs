namespace MtgDecker.Engine.Triggers.Effects;

public class PutCreatureFromHandEffect(string subtype) : IEffect
{
    public async Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var matches = context.Controller.Hand.Cards
            .Where(c => c.IsCreature && c.Subtypes.Contains(subtype, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 0)
        {
            context.State.Log($"No {subtype} creatures in hand.");
            return;
        }

        var chosenId = await context.DecisionHandler.ChooseCard(
            matches, $"Put a {subtype} onto the battlefield", optional: true, ct);

        if (chosenId.HasValue)
        {
            var chosen = context.Controller.Hand.RemoveById(chosenId.Value);
            if (chosen != null)
            {
                context.Controller.Battlefield.Add(chosen);
                chosen.TurnEnteredBattlefield = context.State.TurnNumber;
                if (chosen.EntersTapped) chosen.IsTapped = true;
                context.State.Log($"{context.Controller.Name} puts {chosen.Name} onto the battlefield.");
            }
        }
    }
}
