using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine.Effects;

public class BlueElementalBlastEffect : SpellEffect
{
    private static bool IsRed(ManaCost? cost) =>
        cost != null && cost.ColorRequirements.ContainsKey(ManaColor.Red);

    public override void Resolve(GameState state, StackObject spell)
    {
        if (spell.Targets.Count == 0) return;
        var target = spell.Targets[0];

        // Check if target is on the stack (counter it)
        var stackSpell = state.Stack
            .OfType<StackObject>()
            .FirstOrDefault(s => s.Card.Id == target.CardId);

        if (stackSpell != null)
        {
            if (IsRed(stackSpell.Card.ManaCost))
            {
                state.StackRemove(stackSpell);
                var controller = state.GetPlayer(stackSpell.ControllerId);
                controller.Graveyard.Add(stackSpell.Card);
                state.Log($"{stackSpell.Card.Name} is countered by {spell.Card.Name}.");
            }
            else
            {
                state.Log($"{spell.Card.Name} fizzles (target is not red).");
            }
            return;
        }

        // Check if target is on battlefield (destroy it)
        var owner = state.GetPlayer(target.PlayerId);
        var card = owner.Battlefield.Cards.FirstOrDefault(c => c.Id == target.CardId);
        if (card != null && IsRed(card.ManaCost))
        {
            owner.Battlefield.RemoveById(card.Id);
            owner.Graveyard.Add(card);
            state.Log($"{card.Name} is destroyed by {spell.Card.Name}.");
        }
        else
        {
            state.Log($"{spell.Card.Name} fizzles (target is not red or no longer valid).");
        }
    }
}
