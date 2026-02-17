namespace MtgDecker.Engine.Effects;

/// <summary>
/// Gerrard's Verdict -- Target player discards two cards.
/// You gain 3 life for each land card discarded this way.
/// </summary>
public class GerrardVerdictEffect : SpellEffect
{
    public override async Task ResolveAsync(GameState state, StackObject spell,
        IPlayerDecisionHandler handler, CancellationToken ct = default)
    {
        if (spell.Targets.Count == 0) return;

        var targetPlayer = state.GetPlayer(spell.Targets[0].PlayerId);
        var caster = state.GetPlayer(spell.ControllerId);

        var discardCount = Math.Min(2, targetPlayer.Hand.Count);

        if (discardCount == 0)
        {
            state.Log($"{targetPlayer.Name} has no cards to discard ({spell.Card.Name}).");
            return;
        }

        var landsDiscarded = 0;

        for (int i = 0; i < discardCount; i++)
        {
            var handCards = targetPlayer.Hand.Cards;
            if (handCards.Count == 0) break;

            var chosenId = await targetPlayer.DecisionHandler.ChooseCard(
                handCards, "Choose a card to discard", ct: ct);

            if (!chosenId.HasValue) continue;

            var card = targetPlayer.Hand.RemoveById(chosenId.Value);
            if (card != null)
            {
                targetPlayer.Graveyard.Add(card);
                state.Log($"{targetPlayer.Name} discards {card.Name}.");

                if (card.IsLand)
                    landsDiscarded++;
            }
        }

        if (landsDiscarded > 0)
        {
            var lifeGain = landsDiscarded * 3;
            caster.AdjustLife(lifeGain);
            state.Log($"{caster.Name} gains {lifeGain} life ({landsDiscarded} land(s) discarded, {spell.Card.Name}). Life: {caster.Life}.");
        }
    }
}
