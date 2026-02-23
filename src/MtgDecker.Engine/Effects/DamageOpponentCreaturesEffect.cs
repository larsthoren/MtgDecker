namespace MtgDecker.Engine.Effects;

public class DamageOpponentCreaturesEffect : SpellEffect
{
    public int Amount { get; }

    public DamageOpponentCreaturesEffect(int amount) => Amount = amount;

    public override void Resolve(GameState state, StackObject spell)
    {
        var opponent = state.GetOpponent(state.GetPlayer(spell.ControllerId));
        foreach (var creature in opponent.Battlefield.Cards.Where(c => c.IsCreature))
        {
            creature.DamageMarked += Amount;
        }
        state.Log($"{spell.Card.Name} deals {Amount} damage to each creature {opponent.Name} controls.");
    }
}
