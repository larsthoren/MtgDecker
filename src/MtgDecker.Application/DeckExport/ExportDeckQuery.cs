using MediatR;
using MtgDecker.Application.Interfaces;
using MtgDecker.Domain.Enums;

namespace MtgDecker.Application.DeckExport;

public record ExportDeckQuery(Guid DeckId, string Format) : IRequest<string>;

public class ExportDeckHandler : IRequestHandler<ExportDeckQuery, string>
{
    private readonly IDeckRepository _deckRepository;
    private readonly ICardRepository _cardRepository;

    public ExportDeckHandler(IDeckRepository deckRepository, ICardRepository cardRepository)
    {
        _deckRepository = deckRepository;
        _cardRepository = cardRepository;
    }

    public async Task<string> Handle(ExportDeckQuery request, CancellationToken cancellationToken)
    {
        var deck = await _deckRepository.GetByIdAsync(request.DeckId, cancellationToken)
            ?? throw new KeyNotFoundException($"Deck {request.DeckId} not found.");

        var cardIds = deck.Entries.Select(e => e.CardId).Distinct().ToList();
        var cards = (await _cardRepository.GetByIdsAsync(cardIds, cancellationToken))
            .ToDictionary(c => c.Id);

        var lines = new List<string>();

        var mainDeck = deck.Entries.Where(e => e.Category == DeckCategory.MainDeck);
        var sideboard = deck.Entries.Where(e => e.Category == DeckCategory.Sideboard);
        var maybeboard = deck.Entries.Where(e => e.Category == DeckCategory.Maybeboard);

        if (request.Format.Equals("Arena", StringComparison.OrdinalIgnoreCase))
        {
            lines.Add("Deck");
            foreach (var entry in mainDeck)
            {
                if (cards.TryGetValue(entry.CardId, out var card))
                {
                    var setCode = card.SetCode.ToUpperInvariant();
                    lines.Add($"{entry.Quantity} {card.Name} ({setCode}) {card.CollectorNumber}");
                }
            }

            if (sideboard.Any())
            {
                lines.Add("");
                lines.Add("Sideboard");
                foreach (var entry in sideboard)
                {
                    if (cards.TryGetValue(entry.CardId, out var card))
                    {
                        var setCode = card.SetCode.ToUpperInvariant();
                        lines.Add($"{entry.Quantity} {card.Name} ({setCode}) {card.CollectorNumber}");
                    }
                }
            }

            if (maybeboard.Any())
            {
                lines.Add("");
                lines.Add("Maybeboard");
                foreach (var entry in maybeboard)
                {
                    if (cards.TryGetValue(entry.CardId, out var card))
                    {
                        var setCode = card.SetCode.ToUpperInvariant();
                        lines.Add($"{entry.Quantity} {card.Name} ({setCode}) {card.CollectorNumber}");
                    }
                }
            }
        }
        else // MTGO format
        {
            foreach (var entry in mainDeck)
            {
                if (cards.TryGetValue(entry.CardId, out var card))
                    lines.Add($"{entry.Quantity} {card.Name}");
            }

            foreach (var entry in sideboard)
            {
                if (cards.TryGetValue(entry.CardId, out var card))
                    lines.Add($"SB: {entry.Quantity} {card.Name}");
            }

            foreach (var entry in maybeboard)
            {
                if (cards.TryGetValue(entry.CardId, out var card))
                    lines.Add($"MB: {entry.Quantity} {card.Name}");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }
}
