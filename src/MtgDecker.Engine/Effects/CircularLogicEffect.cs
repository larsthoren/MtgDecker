namespace MtgDecker.Engine.Effects;

/// <summary>
/// Circular Logic — Counter target spell unless its controller pays {1} for each card
/// in the caster's graveyard. Opponent is asked whether they want to pay.
/// </summary>
public class CircularLogicEffect : SpellEffect
{
    public override async Task ResolveAsync(GameState state, StackObject spell,
        IPlayerDecisionHandler handler, CancellationToken ct = default)
    {
        var targetSpell = FindTargetSpellOnStack(state, spell);
        if (targetSpell == null) return;

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
            // Ask opponent whether they want to pay
            var choice = await opponent.DecisionHandler.ChooseCard(
                [targetSpell.Card], $"Pay {{{graveyardCount}}} to prevent counter?", optional: true, ct: ct);

            if (choice.HasValue)
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
                return;
            }
        }

        // Opponent cannot or chose not to pay — counter the spell
        state.StackRemove(targetSpell);
        opponent.Graveyard.Add(targetSpell.Card);
        state.Log($"{targetSpell.Card.Name} is countered by {spell.Card.Name} (unable to pay {{{graveyardCount}}}).");
    }
}
