namespace MtgDecker.Engine.Effects;

public class DamageAllPlayersEffect : SpellEffect
{
    public int Amount { get; }

    public DamageAllPlayersEffect(int amount) => Amount = amount;

    public override void Resolve(GameState state, StackObject spell)
    {
        state.Player1.AdjustLife(-Amount);
        state.Player2.AdjustLife(-Amount);
        state.Log($"{spell.Card.Name} deals {Amount} damage to each player. " +
            $"({state.Player1.Name}: {state.Player1.Life}, {state.Player2.Name}: {state.Player2.Life})");
    }
}
