using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Effects;

/// <summary>
/// Earthquake - Earthquake deals X damage to each creature without flying and each player.
/// X is determined by the remaining mana in the controller's mana pool after paying the base cost.
/// </summary>
public class EarthquakeEffect : SpellEffect
{
    public override void Resolve(GameState state, StackObject spell)
    {
        var controller = state.GetPlayer(spell.ControllerId);
        var pool = controller.ManaPool;
        var x = pool.Total;

        if (x == 0)
        {
            state.Log($"{controller.Name} casts Earthquake with X=0.");
            return;
        }

        // Drain all remaining mana
        foreach (var (color, amount) in pool.Available.ToList())
            pool.Deduct(color, amount);

        // Deal X damage to each creature without flying
        foreach (var player in new[] { state.Player1, state.Player2 })
        {
            foreach (var creature in player.Battlefield.Cards.Where(c => c.IsCreature))
            {
                if (!creature.ActiveKeywords.Contains(Keyword.Flying))
                    creature.DamageMarked += x;
            }
        }

        // Deal X damage to each player
        state.Player1.AdjustLife(-x);
        state.Player2.AdjustLife(-x);

        state.Log($"Earthquake deals {x} damage to each creature without flying and each player. " +
            $"({state.Player1.Name}: {state.Player1.Life}, {state.Player2.Name}: {state.Player2.Life})");
    }
}
