using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Effects;

/// <summary>
/// Deals 2 damage to target creature. If that creature dies this turn,
/// deals 3 damage to its controller via a delayed trigger.
/// </summary>
public class SearingBloodEffect : SpellEffect
{
    public override void Resolve(GameState state, StackObject spell)
    {
        if (spell.Targets.Count == 0) return;
        var target = spell.Targets[0];

        var owner = state.GetPlayer(target.PlayerId);
        var creature = owner.Battlefield.Cards.FirstOrDefault(c => c.Id == target.CardId);
        if (creature == null) return; // target removed = fizzle

        creature.DamageMarked += 2;
        state.Log($"Searing Blood deals 2 damage to {creature.Name}.");

        // Register delayed trigger: if this creature dies this turn, deal 3 to its controller
        var delayed = new DelayedTrigger(
            GameEvent.Dies,
            new DealDamageToPlayerEffect(3, target.PlayerId),
            spell.ControllerId,
            TargetCardId: target.CardId);
        state.DelayedTriggers.Add(delayed);
    }
}
