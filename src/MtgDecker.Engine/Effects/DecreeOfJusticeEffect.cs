using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine.Effects;

/// <summary>
/// Decree of Justice main cast effect: spends remaining mana pool at 2 mana per Angel,
/// creating X 4/4 white Angel creature tokens where X = floor(pool.Total / 2).
/// </summary>
public class DecreeOfJusticeEffect : SpellEffect
{
    public override Task ResolveAsync(GameState state, StackObject spell,
        IPlayerDecisionHandler handler, CancellationToken ct = default)
    {
        var controller = state.GetPlayer(spell.ControllerId);
        var pool = controller.ManaPool;
        var x = pool.Total / 2;

        if (x == 0)
        {
            state.Log($"{controller.Name} casts Decree of Justice but has no mana to pay for X.");
            return Task.CompletedTask;
        }

        // Deduct 2*X mana from pool (drain each color proportionally)
        var manaToDrain = x * 2;
        foreach (var (color, amount) in pool.Available.ToList())
        {
            var deduct = Math.Min(amount, manaToDrain);
            pool.Deduct(color, deduct);
            manaToDrain -= deduct;
            if (manaToDrain <= 0) break;
        }

        for (int i = 0; i < x; i++)
        {
            var token = new GameCard
            {
                Name = "Angel",
                BasePower = 4,
                BaseToughness = 4,
                CardTypes = CardType.Creature,
                Subtypes = ["Angel"],
                IsToken = true,
                TurnEnteredBattlefield = state.TurnNumber,
                Colors = { ManaColor.White },
            };
            controller.Battlefield.Add(token);
        }

        state.Log($"{controller.Name} creates {x} Angel token(s) (4/4) with Decree of Justice.");

        return Task.CompletedTask;
    }
}
