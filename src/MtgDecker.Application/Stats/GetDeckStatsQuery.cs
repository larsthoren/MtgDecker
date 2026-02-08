using FluentValidation;
using MediatR;
using MtgDecker.Application.Interfaces;
using MtgDecker.Domain.Enums;

namespace MtgDecker.Application.Stats;

public record GetDeckStatsQuery(Guid DeckId) : IRequest<DeckStats>;

public class GetDeckStatsValidator : AbstractValidator<GetDeckStatsQuery>
{
    public GetDeckStatsValidator()
    {
        RuleFor(x => x.DeckId).NotEmpty();
    }
}

public record DeckStats
{
    public required int TotalCards { get; init; }
    public required int MainDeckCount { get; init; }
    public required int SideboardCount { get; init; }
    public required Dictionary<int, int> ManaCurve { get; init; }
    public required Dictionary<string, int> ColorDistribution { get; init; }
    public required Dictionary<string, int> TypeBreakdown { get; init; }
    public required decimal TotalPriceUsd { get; init; }
    public required double AverageCmc { get; init; }
    public required int LandCount { get; init; }
    public required int SpellCount { get; init; }
    public required Dictionary<string, int> RarityBreakdown { get; init; }
}

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
        var rarityBd = new Dictionary<string, int>();
        double totalCmc = 0;
        int nonLandCardCount = 0;
        int landCount = 0;
        int spellCount = 0;

        foreach (var entry in deck.Entries.Where(e => e.Category == DeckCategory.MainDeck))
        {
            if (!cards.TryGetValue(entry.CardId, out var card)) continue;

            bool isLand = card.TypeLine.Contains("Land", StringComparison.OrdinalIgnoreCase);

            // Mana curve (lands excluded)
            if (!isLand)
            {
                var cmcKey = (int)Math.Min(card.Cmc, 7); // 7+ grouped
                manaCurve[cmcKey] = manaCurve.GetValueOrDefault(cmcKey) + entry.Quantity;
            }

            // Land vs spell count
            if (isLand)
                landCount += entry.Quantity;
            else
                spellCount += entry.Quantity;

            // Average CMC (exclude lands)
            if (!isLand)
            {
                totalCmc += card.Cmc * entry.Quantity;
                nonLandCardCount += entry.Quantity;
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

            // Rarity breakdown
            if (!string.IsNullOrEmpty(card.Rarity))
            {
                var rarity = card.Rarity.ToLowerInvariant();
                rarityBd[rarity] = rarityBd.GetValueOrDefault(rarity) + entry.Quantity;
            }
        }

        var countableEntries = deck.Entries.Where(e => e.Category != DeckCategory.Maybeboard).ToList();

        var totalPrice = 0m;
        foreach (var entry in countableEntries)
        {
            if (cards.TryGetValue(entry.CardId, out var priceCard) && priceCard.PriceUsd.HasValue)
                totalPrice += priceCard.PriceUsd.Value * entry.Quantity;
        }

        return new DeckStats
        {
            TotalCards = countableEntries.Sum(e => e.Quantity),
            MainDeckCount = deck.TotalMainDeckCount,
            SideboardCount = deck.TotalSideboardCount,
            ManaCurve = manaCurve,
            ColorDistribution = colorDist,
            TypeBreakdown = typeBd,
            TotalPriceUsd = totalPrice,
            AverageCmc = nonLandCardCount > 0 ? totalCmc / nonLandCardCount : 0,
            LandCount = landCount,
            SpellCount = spellCount,
            RarityBreakdown = rarityBd
        };
    }
}
