using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Triggers.Effects;

/// <summary>
/// Tamiyo, Seasoned Scholar -3 ability:
/// Return target instant or sorcery card from your graveyard to your hand.
/// </summary>
public class TamiyoRecoverEffect : IEffect
{
    public async Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var controller = context.Controller;
        var state = context.State;

        var eligible = controller.Graveyard.Cards
            .Where(c => c.CardTypes.HasFlag(CardType.Instant) || c.CardTypes.HasFlag(CardType.Sorcery))
            .ToList();

        if (eligible.Count == 0)
        {
            state.Log("No instant or sorcery cards in graveyard.");
            return;
        }

        var targetId = await context.DecisionHandler.ChooseCard(
            eligible, "Choose an instant or sorcery to return to hand.", optional: true, ct);
        if (!targetId.HasValue) return;

        var target = eligible.FirstOrDefault(c => c.Id == targetId.Value);
        if (target == null) return;

        controller.Graveyard.Remove(target);
        controller.Hand.Add(target);
        state.Log($"{target.Name} returned from graveyard to hand.");
    }
}
