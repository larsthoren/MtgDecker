using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Effects;

/// <summary>
/// Stifle: Counter target activated or triggered ability.
/// Simplified: removes the topmost TriggeredAbilityStackObject from the stack.
/// </summary>
public class StifleEffect : SpellEffect
{
    public override void Resolve(GameState state, StackObject spell)
    {
        // Find the first triggered ability on the stack
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
