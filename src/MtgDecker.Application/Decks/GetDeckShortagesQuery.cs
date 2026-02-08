using MediatR;
using MtgDecker.Application.Interfaces;
using MtgDecker.Domain.Services;

namespace MtgDecker.Application.Decks;

public record GetDeckShortagesQuery(Guid DeckId, Guid UserId) : IRequest<List<CardShortage>>;

public class GetDeckShortagesHandler : IRequestHandler<GetDeckShortagesQuery, List<CardShortage>>
{
    private readonly IDeckRepository _deckRepository;
    private readonly ICollectionRepository _collectionRepository;
    private readonly ICardRepository _cardRepository;

    public GetDeckShortagesHandler(
        IDeckRepository deckRepository,
        ICollectionRepository collectionRepository,
        ICardRepository cardRepository)
    {
        _deckRepository = deckRepository;
        _collectionRepository = collectionRepository;
        _cardRepository = cardRepository;
    }

    public async Task<List<CardShortage>> Handle(GetDeckShortagesQuery request, CancellationToken cancellationToken)
    {
        var deck = await _deckRepository.GetByIdAsync(request.DeckId, cancellationToken)
            ?? throw new KeyNotFoundException($"Deck {request.DeckId} not found.");

        var collection = await _collectionRepository.GetByUserAsync(request.UserId, cancellationToken);

        var allCardIds = deck.Entries.Select(e => e.CardId)
            .Union(collection.Select(c => c.CardId))
            .Distinct()
            .ToList();

        var cards = await _cardRepository.GetByIdsAsync(allCardIds, cancellationToken);

        return ShortageCalculator.Calculate(deck, collection, cards);
    }
}
