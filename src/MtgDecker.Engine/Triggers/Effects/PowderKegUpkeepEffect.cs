using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Triggers.Effects;

/// <summary>
/// Powder Keg upkeep trigger — You may put a fuse counter on Powder Keg.
/// </summary>
public class PowderKegUpkeepEffect : IEffect
{
    public async Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var cards = new List<GameCard> { context.Source };
        var chosenId = await context.DecisionHandler.ChooseCard(
            cards, "Add a fuse counter to Powder Keg?", optional: true, ct);

        if (chosenId.HasValue)
        {
            context.Source.AddCounters(CounterType.Fuse, 1);
            var count = context.Source.GetCounters(CounterType.Fuse);
            context.State.Log($"Added a fuse counter to Powder Keg ({count} total).");
        }
    }
}
