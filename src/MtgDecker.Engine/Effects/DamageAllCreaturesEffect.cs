namespace MtgDecker.Engine.Effects;

public class DamageAllCreaturesEffect : SpellEffect
{
    public int Amount { get; }

    public DamageAllCreaturesEffect(int amount) => Amount = amount;

    public override void Resolve(GameState state, StackObject spell)
    {
        foreach (var player in new[] { state.Player1, state.Player2 })
        {
            foreach (var creature in player.Battlefield.Cards.Where(c => c.IsCreature))
                creature.DamageMarked += Amount;
        }
        state.Log($"{spell.Card.Name} deals {Amount} damage to all creatures.");
    }
}
