namespace MtgDecker.Engine.Effects;

/// <summary>
/// Surgical Extraction — Choose target card in a graveyard other than a basic land card.
/// Search its owner's graveyard, hand, and library for all cards with the same name
/// and exile them. Then that player shuffles.
/// </summary>
public class SurgicalExtractionEffect : SpellEffect
{
    public override async Task ResolveAsync(GameState state, StackObject spell,
        IPlayerDecisionHandler handler, CancellationToken ct = default)
    {
        var allGraveyardCards = state.Player1.Graveyard.Cards
            .Concat(state.Player2.Graveyard.Cards)
            .Where(c => !c.IsBasicLand)
            .ToList();

        if (allGraveyardCards.Count == 0)
        {
            state.Log("No valid targets in any graveyard for Surgical Extraction.");
            return;
        }

        var chosenId = await handler.ChooseCard(allGraveyardCards,
            "Choose a card from a graveyard to extract (not basic land)", optional: false, ct);

        if (!chosenId.HasValue)
            return;

        // Find which player owns the card
        var card = state.Player1.Graveyard.RemoveById(chosenId.Value);
        var owner = state.Player1;
        if (card == null)
        {
            card = state.Player2.Graveyard.RemoveById(chosenId.Value);
            owner = state.Player2;
        }

        if (card == null)
            return;

        var cardName = card.Name;
        owner.Exile.Add(card);
        state.Log($"{card.Name} is exiled from {owner.Name}'s graveyard (Surgical Extraction).");

        // Exile all other copies from owner's graveyard
        var graveyardCopies = owner.Graveyard.Cards
            .Where(c => string.Equals(c.Name, cardName, StringComparison.OrdinalIgnoreCase))
            .ToList();
        foreach (var copy in graveyardCopies)
        {
            owner.Graveyard.RemoveById(copy.Id);
            owner.Exile.Add(copy);
            state.Log($"{copy.Name} exiled from {owner.Name}'s graveyard.");
        }

        // Exile all copies from owner's hand
        var handCopies = owner.Hand.Cards
            .Where(c => string.Equals(c.Name, cardName, StringComparison.OrdinalIgnoreCase))
            .ToList();
        foreach (var copy in handCopies)
        {
            owner.Hand.RemoveById(copy.Id);
            owner.Exile.Add(copy);
            state.Log($"{copy.Name} exiled from {owner.Name}'s hand.");
        }

        // Exile all copies from owner's library
        var libraryCopies = owner.Library.Cards
            .Where(c => string.Equals(c.Name, cardName, StringComparison.OrdinalIgnoreCase))
            .ToList();
        foreach (var copy in libraryCopies)
        {
            owner.Library.RemoveById(copy.Id);
            owner.Exile.Add(copy);
            state.Log($"{copy.Name} exiled from {owner.Name}'s library.");
        }

        // Shuffle library
        owner.Library.Shuffle();
        state.Log($"{owner.Name} shuffles their library.");
    }
}
