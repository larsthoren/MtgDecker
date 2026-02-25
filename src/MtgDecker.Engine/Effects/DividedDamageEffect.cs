using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Effects;

/// <summary>
/// Deals damage divided among any number of target creatures.
/// Used by Pyrokinesis ("4 damage divided as you choose among any number of target creatures").
/// During resolution, the spell targets opponent's creatures and divides damage among them.
/// </summary>
public class DividedDamageEffect(int totalDamage) : SpellEffect
{
    public int TotalDamage { get; } = totalDamage;

    public override async Task ResolveAsync(GameState state, StackObject spell,
        IPlayerDecisionHandler handler, CancellationToken ct = default)
    {
        var caster = state.GetPlayer(spell.ControllerId);
        var opponent = state.GetOpponent(caster);

        var eligibleCreatures = opponent.Battlefield.Cards
            .Where(c => c.IsCreature)
            .ToList();

        if (eligibleCreatures.Count == 0)
        {
            state.Log($"{spell.Card.Name} has no valid targets.");
            return;
        }

        int damageRemaining = TotalDamage;

        while (damageRemaining > 0 && eligibleCreatures.Count > 0)
        {
            var chosenId = await handler.ChooseCard(
                eligibleCreatures,
                $"{spell.Card.Name}: Assign damage ({damageRemaining} remaining)",
                optional: damageRemaining < TotalDamage, // Must assign at least 1 damage
                ct);

            if (chosenId == null) break;

            var target = eligibleCreatures.FirstOrDefault(c => c.Id == chosenId.Value);
            if (target == null) break;

            // Assign 1 damage at a time; player can pick the same creature again
            target.DamageMarked += 1;
            damageRemaining -= 1;
            state.Log($"{spell.Card.Name} assigns 1 damage to {target.Name}. ({target.DamageMarked} total)");

            // Refresh eligible creatures (re-check in case of state changes)
            eligibleCreatures = opponent.Battlefield.Cards
                .Where(c => c.IsCreature)
                .ToList();
        }

        if (damageRemaining < TotalDamage)
        {
            state.Log($"{spell.Card.Name} deals {TotalDamage - damageRemaining} total damage divided among creatures.");
        }
    }
}
