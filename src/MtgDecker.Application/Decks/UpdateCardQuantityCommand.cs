using MediatR;
using MtgDecker.Application.Interfaces;
using MtgDecker.Domain.Entities;
using MtgDecker.Domain.Enums;

namespace MtgDecker.Application.Decks;

public record UpdateCardQuantityCommand(Guid DeckId, Guid CardId, DeckCategory Category, int Quantity) : IRequest<Deck>;

public class UpdateCardQuantityHandler : IRequestHandler<UpdateCardQuantityCommand, Deck>
{
    private readonly IDeckRepository _deckRepository;

    public UpdateCardQuantityHandler(IDeckRepository deckRepository)
    {
        _deckRepository = deckRepository;
    }

    public async Task<Deck> Handle(UpdateCardQuantityCommand request, CancellationToken cancellationToken)
    {
        var deck = await _deckRepository.GetByIdAsync(request.DeckId, cancellationToken)
            ?? throw new KeyNotFoundException($"Deck {request.DeckId} not found.");

        deck.UpdateCardQuantity(request.CardId, request.Category, request.Quantity);
        await _deckRepository.UpdateAsync(deck, cancellationToken);

        return deck;
    }
}
