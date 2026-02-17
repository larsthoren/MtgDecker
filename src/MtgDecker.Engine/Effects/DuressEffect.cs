namespace MtgDecker.Engine.Effects;

/// <summary>
/// Duress — Target player reveals their hand. You choose a noncreature, nonland
/// card from it. That player discards that card.
/// </summary>
public class DuressEffect : SpellEffect
{
    public override async Task ResolveAsync(GameState state, StackObject spell,
        IPlayerDecisionHandler handler, CancellationToken ct = default)
    {
        if (spell.Targets.Count == 0) return;

        var target = state.GetPlayer(spell.Targets[0].PlayerId);
        var eligible = target.Hand.Cards.Where(c => !c.IsCreature && !c.IsLand).ToList();

        if (eligible.Count == 0)
        {
            state.Log("No eligible cards to discard.");
            return;
        }

        // Reveal the full hand to the target (for UI display)
        await target.DecisionHandler.RevealCards(
            target.Hand.Cards, eligible, "Duress: Revealing hand", ct);

        // Caster chooses a noncreature, nonland card from the eligible set
        var chosenId = await handler.ChooseCard(
            eligible, "Choose a card to discard", optional: false, ct);

        if (chosenId.HasValue)
        {
            var card = target.Hand.RemoveById(chosenId.Value);
            if (card != null)
            {
                target.Graveyard.Add(card);
                state.Log($"{target.Name} discards {card.Name} to Duress.");
            }
        }
    }
}
