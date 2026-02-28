namespace MtgDecker.Engine.Effects;

public class CounterSpellEffect : SpellEffect
{
    public override void Resolve(GameState state, StackObject spell)
    {
        var targetSpell = FindTargetSpellOnStack(state, spell);
        if (targetSpell == null) return;

        // Check if the target spell can't be countered
        if (CardDefinitions.TryGet(targetSpell.Card.Name, out var def) && def.CannotBeCountered)
        {
            state.Log($"{targetSpell.Card.Name} can't be countered.");
            return;
        }

        // Remove from stack
        state.StackRemove(targetSpell);

        // Move countered card to owner's graveyard
        var owner = state.GetPlayer(targetSpell.ControllerId);
        owner.Graveyard.Add(targetSpell.Card);

        state.Log($"{targetSpell.Card.Name} is countered by {spell.Card.Name}.");
    }
}
