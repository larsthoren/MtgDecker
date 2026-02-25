using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Triggers.Effects;

/// <summary>
/// ETB effect that deals a specified amount of damage to a target creature.
/// Target is chosen during resolution via the decision handler.
/// Used by Flametongue Kavu (4 damage to target creature).
/// </summary>
public class DealDamageToTargetCreatureEffect(int amount) : IEffect
{
    public async Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var state = context.State;
        var opponent = state.GetOpponent(context.Controller);

        // Build eligible targets: all creatures on the battlefield
        var eligibleTargets = state.Player1.Battlefield.Cards
            .Concat(state.Player2.Battlefield.Cards)
            .Where(c => c.IsCreature
                && !c.ActiveKeywords.Contains(Keyword.Shroud)
                && !(c.ActiveKeywords.Contains(Keyword.Hexproof)
                    && !context.Controller.Battlefield.Contains(c.Id)))
            .ToList();

        if (eligibleTargets.Count == 0)
        {
            state.Log($"{context.Source.Name}'s ability has no legal targets.");
            return;
        }

        var target = await context.DecisionHandler.ChooseTarget(
            context.Source.Name, eligibleTargets, opponent.Id, ct);

        if (target == null) return;

        var creature = state.Player1.Battlefield.Cards
            .Concat(state.Player2.Battlefield.Cards)
            .FirstOrDefault(c => c.Id == target.CardId);

        if (creature != null)
        {
            creature.DamageMarked += amount;
            state.Log($"{context.Source.Name} deals {amount} damage to {creature.Name}.");
        }
        else
        {
            state.Log($"{context.Source.Name}'s target is no longer on the battlefield.");
        }
    }
}
