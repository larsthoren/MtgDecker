namespace MtgDecker.Engine.Effects;

/// <summary>
/// Price of Progress - Price of Progress deals damage to each player equal to
/// twice the number of nonbasic lands that player controls.
/// </summary>
public class PriceOfProgressEffect : SpellEffect
{
    public override void Resolve(GameState state, StackObject spell)
    {
        foreach (var player in new[] { state.Player1, state.Player2 })
        {
            var nonbasicCount = player.Battlefield.Cards
                .Count(c => c.IsLand && !c.IsBasicLand);
            var damage = nonbasicCount * 2;
            if (damage > 0)
            {
                player.AdjustLife(-damage);
            }
            state.Log($"Price of Progress deals {damage} damage to {player.Name} ({nonbasicCount} nonbasic land(s)). Life: {player.Life}.");
        }
    }
}
