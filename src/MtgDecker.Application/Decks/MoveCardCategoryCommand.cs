using MediatR;
using MtgDecker.Application.Interfaces;
using MtgDecker.Domain.Entities;
using MtgDecker.Domain.Enums;

namespace MtgDecker.Application.Decks;

public record MoveCardCategoryCommand(Guid DeckId, Guid CardId, DeckCategory From, DeckCategory To) : IRequest<Deck>;

public class MoveCardCategoryHandler : IRequestHandler<MoveCardCategoryCommand, Deck>
{
    private readonly IDeckRepository _deckRepository;
    private readonly ICardRepository _cardRepository;

    public MoveCardCategoryHandler(IDeckRepository deckRepository, ICardRepository cardRepository)
    {
        _deckRepository = deckRepository;
        _cardRepository = cardRepository;
    }

    public async Task<Deck> Handle(MoveCardCategoryCommand request, CancellationToken cancellationToken)
    {
        var deck = await _deckRepository.GetByIdAsync(request.DeckId, cancellationToken)
            ?? throw new KeyNotFoundException($"Deck {request.DeckId} not found.");

        var cards = await _cardRepository.GetByIdsAsync(new[] { request.CardId }, cancellationToken);
        var card = cards.FirstOrDefault()
            ?? throw new KeyNotFoundException($"Card {request.CardId} not found.");

        deck.MoveCardCategory(card, request.From, request.To);
        await _deckRepository.UpdateAsync(deck, cancellationToken);

        return deck;
    }
}
