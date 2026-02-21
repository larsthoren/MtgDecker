using FluentValidation;
using MediatR;
using MtgDecker.Application.Interfaces;
using MtgDecker.Domain.Entities;
using MtgDecker.Domain.Enums;

namespace MtgDecker.Application.Decks;

public record UpdateCardQuantityCommand(Guid DeckId, Guid CardId, DeckCategory Category, int Quantity) : IRequest<Deck>;

public class UpdateCardQuantityValidator : AbstractValidator<UpdateCardQuantityCommand>
{
    public UpdateCardQuantityValidator()
    {
        RuleFor(x => x.DeckId).NotEmpty();
        RuleFor(x => x.CardId).NotEmpty();
        RuleFor(x => x.Category).IsInEnum();
        RuleFor(x => x.Quantity).GreaterThanOrEqualTo(1);
    }
}

public class UpdateCardQuantityHandler : IRequestHandler<UpdateCardQuantityCommand, Deck>
{
    private readonly IDeckRepository _deckRepository;
    private readonly ICardRepository _cardRepository;
    private readonly TimeProvider _timeProvider;

    public UpdateCardQuantityHandler(IDeckRepository deckRepository, ICardRepository cardRepository, TimeProvider timeProvider)
    {
        _deckRepository = deckRepository;
        _cardRepository = cardRepository;
        _timeProvider = timeProvider;
    }

    public async Task<Deck> Handle(UpdateCardQuantityCommand request, CancellationToken cancellationToken)
    {
        var deck = await _deckRepository.GetByIdAsync(request.DeckId, cancellationToken)
            ?? throw new KeyNotFoundException($"Deck {request.DeckId} not found.");

        if (deck.IsSystemDeck)
            throw new InvalidOperationException("System decks cannot be modified.");

        var card = await _cardRepository.GetByIdAsync(request.CardId, cancellationToken)
            ?? throw new KeyNotFoundException($"Card {request.CardId} not found.");

        deck.UpdateCardQuantity(card, request.Category, request.Quantity, _timeProvider.GetUtcNow().UtcDateTime);
        await _deckRepository.UpdateAsync(deck, cancellationToken);

        return deck;
    }
}
