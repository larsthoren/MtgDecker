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

public record ImportDeckResult(Deck Deck, List<string> UnresolvedCards);

public class ImportDeckHandler : IRequestHandler<ImportDeckCommand, ImportDeckResult>
{
    private readonly IEnumerable<IDeckParser> _parsers;
    private readonly ICardRepository _cardRepository;
    private readonly IDeckRepository _deckRepository;

    public ImportDeckHandler(
        IEnumerable<IDeckParser> parsers,
        ICardRepository cardRepository,
        IDeckRepository deckRepository)
    {
        _parsers = parsers;
        _cardRepository = cardRepository;
        _deckRepository = deckRepository;
    }

    public async Task<ImportDeckResult> Handle(ImportDeckCommand request, CancellationToken cancellationToken)
    {
        var parser = _parsers.FirstOrDefault(p =>
                p.FormatName.Equals(request.ParserFormat, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"No parser found for format '{request.ParserFormat}'.");

        var parsed = parser.Parse(request.DeckText);

        var deck = new Deck
        {
            Id = Guid.NewGuid(),
            Name = request.DeckName,
            Format = request.DeckFormat,
            UserId = request.UserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var unresolved = new List<string>();

        foreach (var entry in parsed.MainDeck)
        {
            var card = await _cardRepository.GetByNameAsync(entry.CardName, cancellationToken);
            if (card == null)
            {
                unresolved.Add(entry.CardName);
                continue;
            }
            deck.AddCard(card, entry.Quantity, DeckCategory.MainDeck);
        }

        foreach (var entry in parsed.Sideboard)
        {
            var card = await _cardRepository.GetByNameAsync(entry.CardName, cancellationToken);
            if (card == null)
            {
                unresolved.Add(entry.CardName);
                continue;
            }
            deck.AddCard(card, entry.Quantity, DeckCategory.Sideboard);
        }

        await _deckRepository.AddAsync(deck, cancellationToken);

        return new ImportDeckResult(deck, unresolved);
    }
}
