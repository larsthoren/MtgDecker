using MediatR;
using MtgDecker.Application.Interfaces;
using MtgDecker.Domain.Entities;
using MtgDecker.Domain.Enums;

namespace MtgDecker.Application.Decks;

public record RemoveCardFromDeckCommand(Guid DeckId, Guid CardId, DeckCategory Category) : IRequest<Deck>;

public class RemoveCardFromDeckHandler : IRequestHandler<RemoveCardFromDeckCommand, Deck>
{
    private readonly IDeckRepository _deckRepository;

    public RemoveCardFromDeckHandler(IDeckRepository deckRepository)
    {
        _deckRepository = deckRepository;
    }

    public async Task<Deck> Handle(RemoveCardFromDeckCommand request, CancellationToken cancellationToken)
    {
        var deck = await _deckRepository.GetByIdAsync(request.DeckId, cancellationToken)
            ?? throw new KeyNotFoundException($"Deck {request.DeckId} not found.");

        deck.RemoveCard(request.CardId, request.Category);
        await _deckRepository.UpdateAsync(deck, cancellationToken);

        return deck;
    }
}
