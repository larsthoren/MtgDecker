using FluentValidation;
using MediatR;
using MtgDecker.Application.Interfaces;
using MtgDecker.Domain.Entities;
using MtgDecker.Domain.Enums;

namespace MtgDecker.Application.DeckExport;

public record ExportDeckQuery(Guid DeckId, string Format) : IRequest<string>;

public class ExportDeckValidator : AbstractValidator<ExportDeckQuery>
{
    private static readonly string[] ValidFormats = ["Text", "CSV", "MTGO", "Arena"];

    public ExportDeckValidator()
    {
        RuleFor(x => x.DeckId).NotEmpty();
        RuleFor(x => x.Format).NotEmpty()
            .Must(f => ValidFormats.Contains(f, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Format must be one of: Text, CSV, MTGO, Arena.");
    }
}

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
            AddEntries(lines, mainDeck, cards, FormatArenaEntry);
            AddSection(lines, sideboard, cards, "Sideboard", FormatArenaEntry);
            AddSection(lines, maybeboard, cards, "Maybeboard", FormatArenaEntry);
        }
        else if (request.Format.Equals("Text", StringComparison.OrdinalIgnoreCase))
        {
            AddEntries(lines, mainDeck, cards, FormatSimpleEntry);
            AddSection(lines, sideboard, cards, "Sideboard", FormatSimpleEntry);
            AddSection(lines, maybeboard, cards, "Maybeboard", FormatSimpleEntry);
        }
        else if (request.Format.Equals("CSV", StringComparison.OrdinalIgnoreCase))
        {
            lines.Add("Quantity,Name,Set,Category");
            foreach (var entry in deck.Entries)
            {
                if (cards.TryGetValue(entry.CardId, out var card))
                    lines.Add($"{entry.Quantity},{EscapeCsv(card.Name)},{card.SetCode.ToUpperInvariant()},{entry.Category}");
            }
        }
        else // MTGO format
        {
            AddEntries(lines, mainDeck, cards, FormatSimpleEntry);
            AddEntries(lines, sideboard, cards, (e, c) => $"SB: {e.Quantity} {c.Name}");
            AddEntries(lines, maybeboard, cards, (e, c) => $"MB: {e.Quantity} {c.Name}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatArenaEntry(DeckEntry entry, Card card) =>
        $"{entry.Quantity} {card.Name} ({card.SetCode.ToUpperInvariant()}) {card.CollectorNumber}";

    private static string FormatSimpleEntry(DeckEntry entry, Card card) =>
        $"{entry.Quantity} {card.Name}";

    private static string EscapeCsv(string value) =>
        value.Contains(',') || value.Contains('"')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;

    private static void AddEntries(List<string> lines, IEnumerable<DeckEntry> entries,
        Dictionary<Guid, Card> cards, Func<DeckEntry, Card, string> formatter)
    {
        foreach (var entry in entries)
        {
            if (cards.TryGetValue(entry.CardId, out var card))
                lines.Add(formatter(entry, card));
        }
    }

    private static void AddSection(List<string> lines, IEnumerable<DeckEntry> entries,
        Dictionary<Guid, Card> cards, string header, Func<DeckEntry, Card, string> formatter)
    {
        var list = entries.ToList();
        if (list.Count == 0) return;
        lines.Add("");
        lines.Add(header);
        AddEntries(lines, list, cards, formatter);
    }
}
