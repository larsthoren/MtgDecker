namespace MtgDecker.Engine.Effects;

/// <summary>
/// Teferi's Response (simplified): Counter target spell. Draw two cards.
/// Full version targets spells/abilities that target a land, but we simplify.
/// </summary>
public class TeferisResponseEffect : SpellEffect
{
    public override void Resolve(GameState state, StackObject spell)
    {
        // Counter the target spell
        if (spell.Targets.Count > 0)
        {
            var target = spell.Targets[0];
            var targetSpell = state.Stack
                .OfType<StackObject>()
                .FirstOrDefault(s => s.Card.Id == target.CardId);

            if (targetSpell != null)
            {
                if (CardDefinitions.TryGet(targetSpell.Card.Name, out var def) && def.CannotBeCountered)
                {
                    state.Log($"{targetSpell.Card.Name} can't be countered.");
                }
                else
                {
                    state.StackRemove(targetSpell);
                    var owner = state.GetPlayer(targetSpell.ControllerId);
                    owner.Graveyard.Add(targetSpell.Card);
                    state.Log($"{targetSpell.Card.Name} is countered by {spell.Card.Name}.");
                }
            }
            else
            {
                state.Log($"{spell.Card.Name} fizzles (target spell already resolved).");
            }
        }

        // Draw two cards for the caster
        var caster = state.GetPlayer(spell.ControllerId);
        for (int i = 0; i < 2; i++)
        {
            var drawn = caster.Library.DrawFromTop();
            if (drawn != null)
            {
                caster.Hand.Add(drawn);
            }
        }
        state.Log($"{caster.Name} draws 2 cards.");
    }
}
