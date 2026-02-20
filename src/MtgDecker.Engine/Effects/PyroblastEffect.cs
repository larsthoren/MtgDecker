using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine.Effects;

public class PyroblastEffect : SpellEffect
{
    private static bool IsBlue(ManaCost? cost) =>
        cost != null && cost.ColorRequirements.ContainsKey(ManaColor.Blue);

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
            if (IsBlue(stackSpell.Card.ManaCost))
            {
                state.StackRemove(stackSpell);
                var controller = state.GetPlayer(stackSpell.ControllerId);
                controller.Graveyard.Add(stackSpell.Card);
                state.Log($"{stackSpell.Card.Name} is countered by Pyroblast.");
            }
            else
            {
                state.Log($"Pyroblast fizzles (target is not blue).");
            }
            return;
        }

        // Check if target is on battlefield (destroy it)
        var owner = state.GetPlayer(target.PlayerId);
        var card = owner.Battlefield.Cards.FirstOrDefault(c => c.Id == target.CardId);
        if (card != null && IsBlue(card.ManaCost))
        {
            owner.Battlefield.RemoveById(card.Id);
            owner.Graveyard.Add(card);
            state.Log($"{card.Name} is destroyed by Pyroblast.");
        }
        else
        {
            state.Log($"Pyroblast fizzles (target is not blue or no longer valid).");
        }
    }
}
