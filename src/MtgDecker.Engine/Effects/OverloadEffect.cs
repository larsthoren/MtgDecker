namespace MtgDecker.Engine.Effects;

/// <summary>
/// Overload: Destroy target artifact if its mana value is 2 or less.
/// If kicked, destroy if mana value is 5 or less instead.
/// </summary>
public class OverloadEffect : SpellEffect
{
    public override void Resolve(GameState state, StackObject spell)
    {
        if (spell.Targets.Count == 0) return;
        var target = spell.Targets[0];
        var owner = state.GetPlayer(target.PlayerId);
        var artifact = owner.Battlefield.Cards.FirstOrDefault(c => c.Id == target.CardId);
        if (artifact == null)
        {
            state.Log($"{spell.Card.Name} fizzles (target gone).");
            return;
        }

        var cmc = artifact.ManaCost?.ConvertedManaCost ?? 0;
        var maxCmc = spell.IsKicked ? 5 : 2;

        if (cmc <= maxCmc)
        {
            owner.Battlefield.RemoveById(artifact.Id);
            owner.Graveyard.Add(artifact);
            state.Log($"{spell.Card.Name} destroys {artifact.Name} (MV {cmc} <= {maxCmc}).");
        }
        else
        {
            state.Log($"{spell.Card.Name} can't destroy {artifact.Name} (MV {cmc} > {maxCmc}).");
        }
    }
}
