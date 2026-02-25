using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Effects;

/// <summary>
/// Spinning Darkness - Deals 3 damage to target nonblack creature. You gain 3 life.
/// </summary>
public class SpinningDarknessEffect : SpellEffect
{
    public override void Resolve(GameState state, StackObject spell)
    {
        var caster = state.GetPlayer(spell.ControllerId);

        if (spell.Targets.Count > 0)
        {
            var target = spell.Targets[0];
            var owner = state.GetPlayer(target.PlayerId);
            var creature = owner.Battlefield.Cards.FirstOrDefault(c => c.Id == target.CardId);
            if (creature != null)
            {
                creature.DamageMarked += 3;
                state.Log($"{spell.Card.Name} deals 3 damage to {creature.Name}. ({creature.DamageMarked} total damage)");
            }
        }

        // Gain 3 life regardless of whether target fizzled
        caster.AdjustLife(3);
        state.Log($"{caster.Name} gains 3 life. ({caster.Life} life)");
    }
}
