namespace MtgDecker.Engine.Effects;

public class CounterSpellEffect : SpellEffect
{
    public override void Resolve(GameState state, StackObject spell)
    {
        if (spell.Targets.Count == 0) return;
        var target = spell.Targets[0];

        // Find the targeted spell on the stack
        var targetSpell = state.Stack.FirstOrDefault(s => s.Card.Id == target.CardId);
        if (targetSpell == null)
        {
            state.Log($"{spell.Card.Name} fizzles (target spell already resolved).");
            return;
        }

        // Remove from stack
        state.Stack.Remove(targetSpell);

        // Move countered card to owner's graveyard
        var owner = targetSpell.ControllerId == state.Player1.Id ? state.Player1 : state.Player2;
        owner.Graveyard.Add(targetSpell.Card);

        state.Log($"{targetSpell.Card.Name} is countered by {spell.Card.Name}.");
    }
}
