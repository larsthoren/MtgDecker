using MediatR;
using Microsoft.Extensions.Logging;
using MtgDecker.Application.Interfaces;

namespace MtgDecker.Application.DeckExport;

public record SeedPresetCardDataCommand() : IRequest<SeedPresetCardDataResult>;

public record SeedPresetCardDataResult(int SeededCount, List<string> NotFoundOnScryfall);

public class SeedPresetCardDataHandler : IRequestHandler<SeedPresetCardDataCommand, SeedPresetCardDataResult>
{
    private readonly ICardRepository _cardRepository;
    private readonly IScryfallClient _scryfallClient;
    private readonly ILogger<SeedPresetCardDataHandler> _logger;

    public SeedPresetCardDataHandler(
        ICardRepository cardRepository,
        IScryfallClient scryfallClient,
        ILogger<SeedPresetCardDataHandler> logger)
    {
        _cardRepository = cardRepository;
        _scryfallClient = scryfallClient;
        _logger = logger;
    }

    public async Task<SeedPresetCardDataResult> Handle(
        SeedPresetCardDataCommand request, CancellationToken cancellationToken)
    {
        // 1. Collect all unique card names from preset decks
        var allNames = PresetDeckRegistry.All
            .SelectMany(d => ParseCardNames(d.DeckTextMtgo))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // 2. Check which cards already exist in DB
        var existingCards = await _cardRepository.GetByNamesAsync(allNames, cancellationToken);
        var existingNames = existingCards.Select(c => c.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missingNames = allNames.Where(n => !existingNames.Contains(n)).ToList();

        if (missingNames.Count == 0)
        {
            _logger.LogInformation("All {Count} preset deck cards already exist in database", allNames.Count);
            return new SeedPresetCardDataResult(0, new List<string>());
        }

        _logger.LogInformation("Fetching {Count} missing cards from Scryfall", missingNames.Count);

        // 3. Fetch missing cards from Scryfall
        var (fetchedCards, notFound) = await _scryfallClient.FetchCardsByNamesAsync(missingNames, cancellationToken);

        // 4. Upsert fetched cards
        if (fetchedCards.Count > 0)
        {
            await _cardRepository.UpsertBatchAsync(fetchedCards, cancellationToken);
            _logger.LogInformation("Seeded {Count} cards from Scryfall", fetchedCards.Count);
        }

        if (notFound.Count > 0)
        {
            _logger.LogWarning("Cards not found on Scryfall: {Cards}", string.Join(", ", notFound));
        }

        return new SeedPresetCardDataResult(fetchedCards.Count, notFound);
    }

    private static IEnumerable<string> ParseCardNames(string deckText)
    {
        foreach (var line in deckText.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;

            // Remove SB: prefix
            if (trimmed.StartsWith("SB:", StringComparison.OrdinalIgnoreCase))
                trimmed = trimmed[3..].Trim();

            // Format: "4 Card Name" — skip the quantity
            var spaceIndex = trimmed.IndexOf(' ');
            if (spaceIndex > 0 && int.TryParse(trimmed[..spaceIndex], out _))
            {
                yield return trimmed[(spaceIndex + 1)..].Trim();
            }
        }
    }
}
