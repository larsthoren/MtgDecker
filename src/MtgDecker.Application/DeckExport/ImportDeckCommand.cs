using FluentValidation;
using MediatR;
using MtgDecker.Application.Interfaces;
using MtgDecker.Domain.Entities;
using MtgDecker.Domain.Enums;

namespace MtgDecker.Application.DeckExport;

public record ImportDeckCommand(
    string DeckText,
    string ParserFormat,
    string DeckName,
    Format DeckFormat,
    Guid UserId) : IRequest<ImportDeckResult>;

public class ImportDeckValidator : AbstractValidator<ImportDeckCommand>
{
    public ImportDeckValidator()
    {
        RuleFor(x => x.DeckText).NotEmpty();
        RuleFor(x => x.ParserFormat).NotEmpty();
        RuleFor(x => x.DeckName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DeckFormat).IsInEnum();
        RuleFor(x => x.UserId).NotEmpty();
    }
}

public record ImportDeckResult(Deck Deck, List<string> UnresolvedCards);

public class ImportDeckHandler : IRequestHandler<ImportDeckCommand, ImportDeckResult>
{
    private readonly IEnumerable<IDeckParser> _parsers;
    private readonly ICardRepository _cardRepository;
    private readonly IDeckRepository _deckRepository;
    private readonly TimeProvider _timeProvider;

    public ImportDeckHandler(
        IEnumerable<IDeckParser> parsers,
        ICardRepository cardRepository,
        IDeckRepository deckRepository,
        TimeProvider timeProvider)
    {
        _parsers = parsers;
        _cardRepository = cardRepository;
        _deckRepository = deckRepository;
        _timeProvider = timeProvider;
    }

    public async Task<ImportDeckResult> Handle(ImportDeckCommand request, CancellationToken cancellationToken)
    {
        var parser = _parsers.FirstOrDefault(p =>
                p.FormatName.Equals(request.ParserFormat, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"No parser found for format '{request.ParserFormat}'.");

        var parsed = parser.Parse(request.DeckText);
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;

        var deck = new Deck
        {
            Id = Guid.NewGuid(),
            Name = request.DeckName,
            Format = request.DeckFormat,
            UserId = request.UserId,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };

        // Batch-fetch all card names in a single query to avoid N+1
        var allNames = parsed.MainDeck.Concat(parsed.Sideboard)
            .Select(e => e.CardName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var cards = await _cardRepository.GetByNamesAsync(allNames, cancellationToken);
        var cardsByName = cards
            .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var unresolved = new List<string>();

        foreach (var entry in parsed.MainDeck)
        {
            if (!cardsByName.TryGetValue(entry.CardName, out var card))
            {
                unresolved.Add(entry.CardName);
                continue;
            }
            deck.AddCard(card, entry.Quantity, DeckCategory.MainDeck, utcNow);
        }

        foreach (var entry in parsed.Sideboard)
        {
            if (!cardsByName.TryGetValue(entry.CardName, out var card))
            {
                unresolved.Add(entry.CardName);
                continue;
            }
            deck.AddCard(card, entry.Quantity, DeckCategory.Sideboard, utcNow);
        }

        await _deckRepository.AddAsync(deck, cancellationToken);

        return new ImportDeckResult(deck, unresolved);
    }
}
