using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine.Triggers.Effects;

/// <summary>
/// Decree of Justice cycling trigger effect: spends remaining mana pool at 1 mana per Soldier,
/// creating X 1/1 white Soldier creature tokens where X = pool.Total.
/// </summary>
public class DecreeOfJusticeCyclingEffect : IEffect
{
    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var controller = context.Controller;
        var pool = controller.ManaPool;
        var x = pool.Total;

        if (x == 0)
        {
            context.State.Log($"{controller.Name} cycles Decree of Justice but has no mana to pay for X.");
            return Task.CompletedTask;
        }

        // Drain all mana from pool
        foreach (var (color, amount) in pool.Available.ToList())
        {
            pool.Deduct(color, amount);
        }

        for (int i = 0; i < x; i++)
        {
            var token = new GameCard
            {
                Name = "Soldier",
                BasePower = 1,
                BaseToughness = 1,
                CardTypes = CardType.Creature,
                Subtypes = ["Soldier"],
                IsToken = true,
                TurnEnteredBattlefield = context.State.TurnNumber,
                Colors = { ManaColor.White },
            };
            controller.Battlefield.Add(token);
        }

        context.State.Log($"{controller.Name} creates {x} Soldier token(s) (1/1) with Decree of Justice cycling trigger.");

        return Task.CompletedTask;
    }
}
