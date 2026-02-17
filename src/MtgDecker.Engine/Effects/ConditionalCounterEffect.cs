namespace MtgDecker.Engine.Effects;

public class ConditionalCounterEffect : SpellEffect
{
    public int GenericCost { get; }

    public ConditionalCounterEffect(int genericCost)
    {
        GenericCost = genericCost;
    }

    public override void Resolve(GameState state, StackObject spell)
    {
        if (spell.Targets.Count == 0) return;
        var target = spell.Targets[0];

        // Find the targeted spell on the stack
        var targetSpell = state.Stack
            .OfType<StackObject>()
            .FirstOrDefault(s => s.Card.Id == target.CardId);
        if (targetSpell == null)
        {
            state.Log($"{spell.Card.Name} fizzles (target spell already resolved).");
            return;
        }

        var opponent = state.GetPlayer(targetSpell.ControllerId);

        // Check if opponent can pay the generic cost
        if (opponent.ManaPool.Total >= GenericCost)
        {
            // Opponent pays — deduct generic mana from their pool
            var remaining = GenericCost;
            var available = opponent.ManaPool.Available;
            foreach (var (color, amount) in available)
            {
                var take = Math.Min(remaining, amount);
                opponent.ManaPool.Deduct(color, take);
                remaining -= take;
                if (remaining <= 0) break;
            }

            state.Log($"{opponent.Name} pays {{{GenericCost}}} \u2014 {targetSpell.Card.Name} resolves.");
        }
        else
        {
            // Opponent cannot pay — counter the spell
            state.StackRemove(targetSpell);
            opponent.Graveyard.Add(targetSpell.Card);
            state.Log($"{targetSpell.Card.Name} is countered by {spell.Card.Name} (unable to pay {{{GenericCost}}}).");
        }
    }
}
