namespace MtgDecker.Engine.Effects;

/// <summary>
/// Orim's Chant: Target player can't cast spells this turn.
/// If kicked, creatures can't attack this turn.
/// </summary>
public class OrimsChantEffect : SpellEffect
{
    public override void Resolve(GameState state, StackObject spell)
    {
        if (spell.Targets.Count == 0) return;
        var target = spell.Targets[0];
        var targetPlayer = state.GetPlayer(target.PlayerId);
        var targetPlayerId = targetPlayer.Id;

        // Prevent the target player from casting spells
        var preventCast = new ContinuousEffect(
            spell.Card.Id,
            ContinuousEffectType.PreventSpellCasting,
            (_, player) => player.Id == targetPlayerId,
            UntilEndOfTurn: true);
        state.ActiveEffects.Add(preventCast);
        state.Log($"{targetPlayer.Name} can't cast spells this turn.");

        // If kicked, creatures can't attack this turn
        if (spell.IsKicked)
        {
            var preventAttack = new ContinuousEffect(
                spell.Card.Id,
                ContinuousEffectType.PreventCreatureAttacks,
                (_, _) => true,
                UntilEndOfTurn: true);
            state.ActiveEffects.Add(preventAttack);
            state.Log($"Creatures can't attack this turn.");
        }
    }
}
