using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Triggers.Effects;

/// <summary>
/// Kaito's -2 ability: Tap target creature. Put two stun counters on it.
/// Filters out Shroud creatures entirely, and filters out opponent's Hexproof creatures
/// (controller can still target their own Hexproof creatures).
/// </summary>
public class TapAndStunEffect : IEffect
{
    public async Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var controller = context.Controller;
        var state = context.State;
        var opponent = state.GetOpponent(controller);

        // Build eligible targets: all creatures on both battlefields,
        // filtered by Shroud (no one can target) and Hexproof (only opponents can't target)
        var eligible = new List<GameCard>();

        foreach (var card in controller.Battlefield.Cards)
        {
            if (!card.IsCreature) continue;
            if (card.ActiveKeywords.Contains(Keyword.Shroud)) continue;
            // Controller CAN target their own Hexproof creatures
            eligible.Add(card);
        }

        foreach (var card in opponent.Battlefield.Cards)
        {
            if (!card.IsCreature) continue;
            if (card.ActiveKeywords.Contains(Keyword.Shroud)) continue;
            if (card.ActiveKeywords.Contains(Keyword.Hexproof)) continue;
            eligible.Add(card);
        }

        if (eligible.Count == 0)
        {
            state.Log("No legal targets for tap and stun.");
            return;
        }

        var targetId = await context.DecisionHandler.ChooseCard(
            eligible,
            "Choose a creature to tap and stun.",
            optional: true, ct);

        if (!targetId.HasValue) return;

        var target = eligible.FirstOrDefault(c => c.Id == targetId.Value);
        if (target == null) return;

        target.IsTapped = true;
        state.Log($"{target.Name} is tapped.");

        target.AddCounters(CounterType.Stun, 2);
        state.Log($"Two stun counters placed on {target.Name}.");
    }
}
