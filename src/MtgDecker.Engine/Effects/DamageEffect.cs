using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Effects;

public class DamageEffect : SpellEffect
{
    public int Amount { get; }
    public bool CanTargetCreature { get; }
    public bool CanTargetPlayer { get; }

    public DamageEffect(int amount, bool canTargetCreature = true, bool canTargetPlayer = true)
    {
        Amount = amount;
        CanTargetCreature = canTargetCreature;
        CanTargetPlayer = canTargetPlayer;
    }

    public override void Resolve(GameState state, StackObject spell)
    {
        if (spell.Targets.Count == 0) return;
        var target = spell.Targets[0];

        if (target.CardId == Guid.Empty && target.Zone == ZoneType.None)
        {
            // Player target
            var player = target.PlayerId == state.Player1.Id ? state.Player1 : state.Player2;
            player.AdjustLife(-Amount);
            state.Log($"{spell.Card.Name} deals {Amount} damage to {player.Name}. ({player.Life} life)");
        }
        else
        {
            // Creature target
            var owner = target.PlayerId == state.Player1.Id ? state.Player1 : state.Player2;
            var creature = owner.Battlefield.Cards.FirstOrDefault(c => c.Id == target.CardId);
            if (creature == null) return; // target removed = fizzle
            creature.DamageMarked += Amount;
            state.Log($"{spell.Card.Name} deals {Amount} damage to {creature.Name}. ({creature.DamageMarked} total damage)");
        }
    }
}
