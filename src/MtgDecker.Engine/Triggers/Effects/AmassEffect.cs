using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Triggers.Effects;

/// <summary>
/// Amass [subtype] N: If you control an Army creature, put N +1/+1 counters on it.
/// Otherwise, create a 0/0 [subtype] Army creature token, then put N +1/+1 counters on it.
/// MTG rule 701.44.
/// </summary>
public class AmassEffect(string subtype, int amount) : IEffect
{
    public string Subtype { get; } = subtype;
    public int Amount { get; } = amount;

    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var army = context.Controller.Battlefield.Cards
            .FirstOrDefault(c => c.IsCreature && c.Subtypes.Contains("Army", StringComparer.OrdinalIgnoreCase));

        if (army == null)
        {
            army = new GameCard
            {
                Name = $"{Subtype} Army",
                BasePower = 0,
                BaseToughness = 0,
                CardTypes = CardType.Creature,
                Subtypes = [Subtype, "Army"],
                IsToken = true,
                TurnEnteredBattlefield = context.State.TurnNumber,
            };
            context.Controller.Battlefield.Add(army);
            context.State.Log($"{context.Controller.Name} creates a {Subtype} Army token (0/0).");
        }

        army.AddCounters(CounterType.PlusOnePlusOne, Amount);
        context.State.Log($"Amass {Subtype} {Amount}: {army.Name} now has {army.GetCounters(CounterType.PlusOnePlusOne)} +1/+1 counter(s).");

        return Task.CompletedTask;
    }
}
