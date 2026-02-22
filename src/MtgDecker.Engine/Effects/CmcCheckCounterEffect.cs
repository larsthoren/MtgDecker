namespace MtgDecker.Engine.Effects;

public class CmcCheckCounterEffect : SpellEffect
{
    public int MaxCmc { get; }

    public CmcCheckCounterEffect(int maxCmc) => MaxCmc = maxCmc;

    public override void Resolve(GameState state, StackObject spell)
    {
        if (spell.Targets.Count == 0) return;
        var target = spell.Targets[0];

        var targetSpell = state.Stack
            .OfType<StackObject>()
            .FirstOrDefault(s => s.Card.Id == target.CardId);
        if (targetSpell == null)
        {
            state.Log($"{spell.Card.Name} fizzles (target spell already resolved).");
            return;
        }

        var cmc = targetSpell.Card.ManaCost?.ConvertedManaCost ?? 0;
        if (cmc <= MaxCmc)
        {
            var owner = state.GetPlayer(targetSpell.ControllerId);
            state.StackRemove(targetSpell);
            owner.Graveyard.Add(targetSpell.Card);
            state.Log($"{targetSpell.Card.Name} (CMC {cmc}) is countered by {spell.Card.Name}.");
        }
        else
        {
            state.Log($"{spell.Card.Name} can't counter {targetSpell.Card.Name} (CMC {cmc} > {MaxCmc}).");
        }
    }
}
