namespace MtgDecker.Engine.Effects;

/// <summary>
/// Thoughtseize — Target player reveals their hand. You choose a nonland card
/// from it. That player discards that card. You lose 2 life.
/// </summary>
public class ThoughtseizeEffect : SpellEffect
{
    public override async Task ResolveAsync(GameState state, StackObject spell,
        IPlayerDecisionHandler handler, CancellationToken ct = default)
    {
        if (spell.Targets.Count == 0) return;

        var caster = state.GetPlayer(spell.ControllerId);
        var target = state.GetPlayer(spell.Targets[0].PlayerId);
        var eligible = target.Hand.Cards
            .Where(c => !c.IsLand)
            .ToList();

        if (eligible.Count == 0)
        {
            state.Log("No nonland cards to discard.");
            // Still lose 2 life
            caster.AdjustLife(-2);
            state.Log($"{caster.Name} loses 2 life from Thoughtseize.");
            return;
        }

        // Reveal the full hand to the target (for UI display)
        await target.DecisionHandler.RevealCards(
            target.Hand.Cards, eligible, "Thoughtseize: Revealing hand", ct);

        // Caster chooses a nonland card from the eligible set
        var chosenId = await handler.ChooseCard(
            eligible, "Choose a nonland card to discard", optional: false, ct);

        if (chosenId.HasValue)
        {
            var card = target.Hand.RemoveById(chosenId.Value);
            if (card != null)
            {
                target.Graveyard.Add(card);
                state.Log($"{target.Name} discards {card.Name} to Thoughtseize.");
            }
        }

        // Caster loses 2 life
        caster.AdjustLife(-2);
        state.Log($"{caster.Name} loses 2 life from Thoughtseize.");
    }
}
