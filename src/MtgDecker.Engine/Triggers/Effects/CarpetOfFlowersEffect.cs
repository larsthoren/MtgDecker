using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Triggers.Effects;

/// <summary>
/// Carpet of Flowers: At the beginning of each of your main phases,
/// add X mana of any one color, where X is the number of Islands
/// target opponent controls. Only once per turn (tracked via GameCard.CarpetUsedThisTurn).
/// </summary>
public class CarpetOfFlowersEffect : IEffect
{
    public async Task Execute(EffectContext context, CancellationToken ct = default)
    {
        // Check if already used this turn
        if (context.Source.CarpetUsedThisTurn)
        {
            return;
        }

        var opponent = context.State.GetOpponent(context.Controller);
        var islandCount = opponent.Battlefield.Cards
            .Count(c => c.Subtypes.Contains("Island", StringComparer.OrdinalIgnoreCase));

        if (islandCount <= 0)
        {
            context.State.Log($"{context.Source.Name}: opponent controls no Islands.");
            return;
        }

        // Choose a color
        var colors = new List<ManaColor>
            { ManaColor.White, ManaColor.Blue, ManaColor.Black, ManaColor.Red, ManaColor.Green };
        var chosen = await context.DecisionHandler.ChooseManaColor(colors, ct);

        // Add X mana of chosen color
        context.Controller.ManaPool.Add(chosen, islandCount);
        context.Source.CarpetUsedThisTurn = true;
        context.State.Log($"{context.Source.Name} adds {islandCount} {chosen} mana ({opponent.Name} controls {islandCount} Island(s)).");
    }
}
