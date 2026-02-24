namespace MtgDecker.Engine.Effects;

/// <summary>
/// Circular Logic — Counter target spell unless its controller pays {1} for each card
/// in the caster's graveyard. Uses the same pay-or-counter pattern as ConditionalCounterEffect
/// but with a dynamic cost based on graveyard size.
/// </summary>
public class CircularLogicEffect : SpellEffect
{
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

        var caster = state.GetPlayer(spell.ControllerId);
        var opponent = state.GetPlayer(targetSpell.ControllerId);

        // Cost = {1} for each card in caster's graveyard
        var graveyardCount = caster.Graveyard.Count;

        if (graveyardCount == 0)
        {
            // No cards in graveyard = free counter resistance
            state.Log($"{targetSpell.Card.Name} resolves — {spell.Card.Name}'s counter cost is 0 (empty graveyard).");
            return;
        }

        // Check if opponent can pay the generic cost
        if (opponent.ManaPool.Total >= graveyardCount)
        {
            // Opponent pays — deduct generic mana from their pool
            var remaining = graveyardCount;
            var available = opponent.ManaPool.Available;
            foreach (var (color, amount) in available)
            {
                var take = Math.Min(remaining, amount);
                opponent.ManaPool.Deduct(color, take);
                remaining -= take;
                if (remaining <= 0) break;
            }

            state.Log($"{opponent.Name} pays {{{graveyardCount}}} — {targetSpell.Card.Name} resolves.");
        }
        else
        {
            // Opponent cannot pay — counter the spell
            state.StackRemove(targetSpell);
            opponent.Graveyard.Add(targetSpell.Card);
            state.Log($"{targetSpell.Card.Name} is countered by {spell.Card.Name} (unable to pay {{{graveyardCount}}}).");
        }
    }
}
