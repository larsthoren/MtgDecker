using MtgDecker.Domain.Entities;

namespace MtgDecker.Domain.Services;

public record CardShortage(string CardName, int Needed, int Owned, int Shortage);

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

        // Aggregate needed quantities by OracleId across all deck categories
        var neededByOracleId = deck.Entries
            .Where(entry => cardsById.ContainsKey(entry.CardId))
            .GroupBy(entry => cardsById[entry.CardId].OracleId)
            .Select(g =>
            {
                var card = cardsById[g.First().CardId];
                var totalNeeded = g.Sum(entry => entry.Quantity);
                var owned = ownedByOracleId.GetValueOrDefault(card.OracleId, 0);
                return new { card.Name, card.OracleId, TotalNeeded = totalNeeded, Owned = owned };
            });

        var shortages = new List<CardShortage>();

        foreach (var item in neededByOracleId)
        {
            var shortage = item.TotalNeeded - item.Owned;
            if (shortage > 0)
            {
                shortages.Add(new CardShortage(item.Name, item.TotalNeeded, item.Owned, shortage));
            }
        }

        return shortages;
    }
}
