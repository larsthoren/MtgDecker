using MediatR;
using MtgDecker.Application.Interfaces;
using MtgDecker.Domain.Enums;

namespace MtgDecker.Application.Stats;

public record GetDeckStatsQuery(Guid DeckId) : IRequest<DeckStats>;

public record DeckStats(
    int TotalCards,
    int MainDeckCount,
    int SideboardCount,
    Dictionary<int, int> ManaCurve,
    Dictionary<string, int> ColorDistribution,
    Dictionary<string, int> TypeBreakdown,
    decimal TotalPriceUsd);

public class GetDeckStatsHandler : IRequestHandler<GetDeckStatsQuery, DeckStats>
{
    private readonly IDeckRepository _deckRepository;
    private readonly ICardRepository _cardRepository;

    public GetDeckStatsHandler(IDeckRepository deckRepository, ICardRepository cardRepository)
    {
        _deckRepository = deckRepository;
        _cardRepository = cardRepository;
    }

    public async Task<DeckStats> Handle(GetDeckStatsQuery request, CancellationToken cancellationToken)
    {
        var deck = await _deckRepository.GetByIdAsync(request.DeckId, cancellationToken)
            ?? throw new KeyNotFoundException($"Deck {request.DeckId} not found.");

        var cardIds = deck.Entries.Select(e => e.CardId).Distinct().ToList();
        var cards = (await _cardRepository.GetByIdsAsync(cardIds, cancellationToken))
            .ToDictionary(c => c.Id);

        var manaCurve = new Dictionary<int, int>();
        var colorDist = new Dictionary<string, int>();
        var typeBd = new Dictionary<string, int>();

        foreach (var entry in deck.Entries.Where(e => e.Category == DeckCategory.MainDeck))
        {
            if (!cards.TryGetValue(entry.CardId, out var card)) continue;

            // Mana curve (lands excluded)
            if (!card.TypeLine.Contains("Land", StringComparison.OrdinalIgnoreCase))
            {
                var cmcKey = (int)Math.Min(card.Cmc, 7); // 7+ grouped
                manaCurve[cmcKey] = manaCurve.GetValueOrDefault(cmcKey) + entry.Quantity;
            }

            // Color distribution
            if (!string.IsNullOrEmpty(card.Colors))
            {
                foreach (var color in card.Colors.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    var c = color.Trim();
                    colorDist[c] = colorDist.GetValueOrDefault(c) + entry.Quantity;
                }
            }

            // Type breakdown (first type before the dash)
            var typeLine = card.TypeLine.Split('—')[0].Trim();
            var mainType = typeLine.Split(' ').Last(); // "Legendary Creature" -> "Creature"
            typeBd[mainType] = typeBd.GetValueOrDefault(mainType) + entry.Quantity;
        }

        var countableEntries = deck.Entries.Where(e => e.Category != DeckCategory.Maybeboard).ToList();

        var totalPrice = 0m;
        foreach (var entry in countableEntries)
        {
            if (cards.TryGetValue(entry.CardId, out var priceCard) && priceCard.PriceUsd.HasValue)
                totalPrice += priceCard.PriceUsd.Value * entry.Quantity;
        }

        return new DeckStats(
            countableEntries.Sum(e => e.Quantity),
            deck.TotalMainDeckCount,
            deck.TotalSideboardCount,
            manaCurve,
            colorDist,
            typeBd,
            totalPrice);
    }
}
