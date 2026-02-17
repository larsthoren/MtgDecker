namespace MtgDecker.Engine.Effects;

/// <summary>
/// Cabal Therapy — The caster sees the target player's hand and chooses a nonland card.
/// All copies of that card (by name) are discarded from the target's hand.
/// </summary>
public class CabalTherapyEffect : SpellEffect
{
    public override async Task ResolveAsync(GameState state, StackObject spell,
        IPlayerDecisionHandler handler, CancellationToken ct = default)
    {
        if (spell.Targets.Count == 0) return;

        var target = spell.Targets[0];
        var targetPlayer = state.GetPlayer(target.PlayerId);

        // Get unique non-land cards in target's hand
        var eligibleCards = targetPlayer.Hand.Cards
            .Where(c => !c.IsLand)
            .ToList();

        if (eligibleCards.Count == 0)
        {
            state.Log("No nonland cards to name.");
            return;
        }

        // Caster chooses a card from the eligible options
        var chosenId = await handler.ChooseCard(eligibleCards, "Name a card", ct: ct);

        if (chosenId == null) return;

        var chosenCard = eligibleCards.FirstOrDefault(c => c.Id == chosenId);
        if (chosenCard == null) return;

        var chosenName = chosenCard.Name;

        // Find ALL cards in target's hand with that name (including lands with that name, though unlikely)
        var toDiscard = targetPlayer.Hand.Cards
            .Where(c => c.Name == chosenName)
            .ToList();

        // Discard each copy
        foreach (var card in toDiscard)
        {
            targetPlayer.Hand.Remove(card);
            targetPlayer.Graveyard.Add(card);
            state.Log($"{targetPlayer.Name} discards {card.Name}.");
        }

        // Reveal hand to the target player (for UI display)
        var remainingHand = targetPlayer.Hand.Cards;
        await targetPlayer.DecisionHandler.RevealCards(remainingHand, remainingHand,
            $"{targetPlayer.Name}'s hand after Cabal Therapy", ct);
    }
}
