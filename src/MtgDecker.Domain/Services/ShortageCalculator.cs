using MtgDecker.Domain.Entities;

namespace MtgDecker.Domain.Services;

public static class ShortageCalculator
{
    public static List<CardShortage> Calculate(
        Deck deck,
        IEnumerable<CollectionEntry> collection,
        IEnumerable<Card> cardLookup)
    {
        var cardsById = cardLookup.ToDictionary(c => c.Id);

        var ownedByOracleId = collection
            .Where(ce => cardsById.ContainsKey(ce.CardId))
            .GroupBy(ce => cardsById[ce.CardId].OracleId)
            .ToDictionary(g => g.Key, g => g.Sum(ce => ce.Quantity));

        var shortages = new List<CardShortage>();

        foreach (var entry in deck.Entries)
        {
            if (!cardsById.TryGetValue(entry.CardId, out var card))
                continue;

            var owned = ownedByOracleId.GetValueOrDefault(card.OracleId, 0);
            var shortage = entry.Quantity - owned;

            if (shortage > 0)
            {
                shortages.Add(new CardShortage(card.Name, entry.Quantity, owned, shortage));
            }
        }

        return shortages;
    }
}

public record CardShortage(string CardName, int Needed, int Owned, int Shortage);
