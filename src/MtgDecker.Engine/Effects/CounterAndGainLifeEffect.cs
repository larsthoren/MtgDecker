namespace MtgDecker.Engine.Effects;

public class CounterAndGainLifeEffect : SpellEffect
{
    private readonly int _lifeGain;

    public CounterAndGainLifeEffect(int lifeGain)
    {
        _lifeGain = lifeGain;
    }

    public override void Resolve(GameState state, StackObject spell)
    {
        var targetSpell = FindTargetSpellOnStack(state, spell);
        if (targetSpell == null) return;

        // Remove from stack
        state.StackRemove(targetSpell);

        // Move countered card to owner's graveyard
        var owner = state.GetPlayer(targetSpell.ControllerId);
        owner.Graveyard.Add(targetSpell.Card);

        // Gain life for the caster
        var caster = state.GetPlayer(spell.ControllerId);
        caster.AdjustLife(_lifeGain);

        state.Log($"{targetSpell.Card.Name} is countered by {spell.Card.Name}. {caster.Name} gains {_lifeGain} life.");
    }
}
