using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Triggers.Effects;

/// <summary>
/// Orcish Bowmasters' combined effect: amass Orcs 1, then deal 1 damage to any target.
/// Target selection happens during resolution via the decision handler.
/// </summary>
public class BowmastersEffect : IEffect
{
    public async Task Execute(EffectContext context, CancellationToken ct = default)
    {
        // Step 1: Amass Orcs 1
        var amass = new AmassEffect("Orc", 1);
        await amass.Execute(context, ct);

        // Step 2: Deal 1 damage to any target (creature or player)
        // Get all eligible creature targets (exclude shroud and opponent hexproof)
        var eligibleCreatures = context.State.Player1.Battlefield.Cards
            .Concat(context.State.Player2.Battlefield.Cards)
            .Where(c => c.IsCreature
                && !c.ActiveKeywords.Contains(Keyword.Shroud)
                && !(c.ActiveKeywords.Contains(Keyword.Hexproof)
                    && !context.Controller.Battlefield.Contains(c.Id)))
            .ToList();

        GameCard? targetCreature = null;
        if (eligibleCreatures.Count > 0)
        {
            var chosenId = await context.DecisionHandler.ChooseCard(
                eligibleCreatures,
                "Choose target for Orcish Bowmasters (1 damage), or decline to target opponent",
                optional: true, ct);

            if (chosenId.HasValue)
                targetCreature = eligibleCreatures.FirstOrDefault(c => c.Id == chosenId.Value);
        }

        if (targetCreature != null)
        {
            targetCreature.DamageMarked += 1;
            context.State.Log($"{context.Source.Name} deals 1 damage to {targetCreature.Name}.");
        }
        else
        {
            // Default: deal 1 damage to opponent (respecting damage prevention)
            var opponent = context.State.GetOpponent(context.Controller);

            var hasDamageProtection = context.State.ActiveEffects.Any(e =>
                e.Type == ContinuousEffectType.PreventDamageToPlayer
                && (context.State.Player1.Battlefield.Contains(e.SourceId)
                    ? context.State.Player1 : context.State.Player2).Id == opponent.Id);

            if (hasDamageProtection)
            {
                context.State.Log($"Damage to {opponent.Name} is prevented (protection).");
            }
            else
            {
                opponent.AdjustLife(-1);
                context.State.Log($"{context.Source.Name} deals 1 damage to {opponent.Name}. ({opponent.Life} life)");
            }
        }
    }
}
