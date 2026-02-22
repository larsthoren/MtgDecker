using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Triggers.Effects;

public class GempalmIncineratorEffect : IEffect
{
    public async Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var goblinCount = context.State.Player1.Battlefield.Cards
            .Concat(context.State.Player2.Battlefield.Cards)
            .Count(c => c.IsCreature && c.Subtypes.Contains("Goblin", StringComparer.OrdinalIgnoreCase));

        if (goblinCount == 0)
        {
            context.State.Log("Gempalm Incinerator deals 0 damage (no Goblins).");
            return;
        }

        var eligible = context.State.Player1.Battlefield.Cards
            .Concat(context.State.Player2.Battlefield.Cards)
            .Where(c => c.IsCreature
                && !c.ActiveKeywords.Contains(Keyword.Shroud)
                && !(c.ActiveKeywords.Contains(Keyword.Hexproof)
                    && !context.Controller.Battlefield.Contains(c.Id)))
            .ToList();

        if (eligible.Count == 0) return;

        var chosenId = await context.DecisionHandler.ChooseCard(
            eligible, $"Choose target for Gempalm Incinerator ({goblinCount} damage)", optional: true, ct);

        if (chosenId.HasValue)
        {
            var target = eligible.FirstOrDefault(c => c.Id == chosenId.Value);
            if (target != null)
            {
                target.DamageMarked += goblinCount;
                context.State.Log($"Gempalm Incinerator deals {goblinCount} damage to {target.Name}.");
            }
        }
    }
}
