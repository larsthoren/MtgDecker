using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Effects;

/// <summary>
/// Earthquake - Earthquake deals X damage to each creature without flying and each player.
/// Player explicitly chooses X from remaining mana pool (1 to pool.Total).
/// </summary>
public class EarthquakeEffect : SpellEffect
{
    public override async Task ResolveAsync(GameState state, StackObject spell,
        IPlayerDecisionHandler handler, CancellationToken ct = default)
    {
        var controller = state.GetPlayer(spell.ControllerId);
        var pool = controller.ManaPool;
        var maxX = pool.Total;

        if (maxX == 0)
        {
            state.Log($"{controller.Name} casts Earthquake with X=0.");
            return;
        }

        // Create options for X values (1 to maxX)
        var options = Enumerable.Range(1, maxX)
            .Select(i => new GameCard { Name = $"X = {i}" })
            .ToList();

        var chosen = await handler.ChooseCard(options, "Choose X for Earthquake", optional: false, ct: ct);

        int x = maxX; // default to max if choice is null or invalid
        if (chosen.HasValue)
        {
            var chosenCard = options.FirstOrDefault(c => c.Id == chosen.Value);
            if (chosenCard != null)
                x = int.Parse(chosenCard.Name.Replace("X = ", ""));
        }

        // Deduct X generic mana (not the entire pool)
        var remaining = x;
        foreach (var (color, amount) in pool.Available.ToList())
        {
            var take = Math.Min(remaining, amount);
            pool.Deduct(color, take);
            remaining -= take;
            if (remaining <= 0) break;
        }

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
