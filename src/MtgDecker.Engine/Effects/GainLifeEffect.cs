namespace MtgDecker.Engine.Effects;

public class GainLifeEffect : SpellEffect
{
    public int Amount { get; }

    public GainLifeEffect(int amount) => Amount = amount;

    public override void Resolve(GameState state, StackObject spell)
    {
        var caster = state.GetPlayer(spell.ControllerId);
        caster.AdjustLife(Amount);
        state.Log($"{caster.Name} gains {Amount} life. ({caster.Life} life)");
    }
}
