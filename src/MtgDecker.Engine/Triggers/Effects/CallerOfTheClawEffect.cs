using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine.Triggers.Effects;

public class CallerOfTheClawEffect : IEffect
{
    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var player = context.Controller;
        var count = player.CreaturesDiedThisTurn;

        if (count <= 0)
        {
            context.State.Log($"Caller of the Claw: No creatures died this turn.");
            return Task.CompletedTask;
        }

        for (int i = 0; i < count; i++)
        {
            var token = new GameCard
            {
                Name = "Bear",
                BasePower = 2,
                BaseToughness = 2,
                CardTypes = CardType.Creature,
                Subtypes = ["Bear"],
                IsToken = true,
                TurnEnteredBattlefield = context.State.TurnNumber,
                Colors = { ManaColor.Green },
            };
            player.Battlefield.Add(token);
        }

        context.State.Log($"Caller of the Claw creates {count} 2/2 Bear token(s).");
        return Task.CompletedTask;
    }
}
