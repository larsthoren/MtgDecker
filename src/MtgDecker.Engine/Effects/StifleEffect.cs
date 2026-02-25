using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Effects;

/// <summary>
/// Stifle: Counter target activated or triggered ability.
/// NOTE: The engine does not put activated abilities on the stack as separate objects —
/// they resolve immediately via ActivateAbilityHandler. So for now, Stifle can only
/// counter triggered abilities (TriggeredAbilityStackObject).
/// </summary>
public class StifleEffect : SpellEffect
{
    public override async Task ResolveAsync(GameState state, StackObject spell,
        IPlayerDecisionHandler handler, CancellationToken ct = default)
    {
        var triggeredAbilities = state.Stack
            .OfType<TriggeredAbilityStackObject>()
            .ToList();

        if (triggeredAbilities.Count == 0)
        {
            state.Log($"{spell.Card.Name} fizzles (no triggered ability on stack).");
            return;
        }

        TriggeredAbilityStackObject target;
        if (triggeredAbilities.Count == 1)
        {
            target = triggeredAbilities[0];
        }
        else
        {
            // Multiple triggered abilities — player chooses which to counter
            var sourceCards = triggeredAbilities.Select(t => t.Source).ToList();
            var chosenId = await handler.ChooseCard(
                sourceCards,
                "Stifle: Choose a triggered ability to counter.",
                optional: false, ct);

            target = chosenId.HasValue
                ? triggeredAbilities.FirstOrDefault(t => t.Source.Id == chosenId.Value)
                  ?? triggeredAbilities[0]
                : triggeredAbilities[0];
        }

        state.StackRemove(target);
        state.Log($"{spell.Card.Name} counters {target.Source.Name}'s triggered ability.");
    }

    // Keep sync Resolve for backward compatibility with existing tests that call it directly
    public override void Resolve(GameState state, StackObject spell)
    {
        var triggeredAbility = state.Stack
            .OfType<TriggeredAbilityStackObject>()
            .FirstOrDefault();

        if (triggeredAbility == null)
        {
            state.Log($"{spell.Card.Name} fizzles (no triggered ability on stack).");
            return;
        }

        state.StackRemove(triggeredAbility);
        state.Log($"{spell.Card.Name} counters {triggeredAbility.Source.Name}'s triggered ability.");
    }
}
