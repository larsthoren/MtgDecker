using FluentValidation;
using MediatR;
using MtgDecker.Application.Interfaces;
using MtgDecker.Domain.Entities;
using MtgDecker.Domain.Enums;

namespace MtgDecker.Application.Decks;

public record AddCardToDeckCommand(Guid DeckId, Guid CardId, int Quantity, DeckCategory Category) : IRequest<Deck>;

public class AddCardToDeckValidator : AbstractValidator<AddCardToDeckCommand>
{
    public AddCardToDeckValidator()
    {
        RuleFor(x => x.DeckId).NotEmpty();
        RuleFor(x => x.CardId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThanOrEqualTo(1);
        RuleFor(x => x.Category).IsInEnum();
    }
}

public class AddCardToDeckHandler : IRequestHandler<AddCardToDeckCommand, Deck>
{
    private readonly IDeckRepository _deckRepository;
    private readonly ICardRepository _cardRepository;
    private readonly TimeProvider _timeProvider;

    public AddCardToDeckHandler(IDeckRepository deckRepository, ICardRepository cardRepository, TimeProvider timeProvider)
    {
        _deckRepository = deckRepository;
        _cardRepository = cardRepository;
        _timeProvider = timeProvider;
    }

    public async Task<Deck> Handle(AddCardToDeckCommand request, CancellationToken cancellationToken)
    {
        var deck = await _deckRepository.GetByIdAsync(request.DeckId, cancellationToken)
            ?? throw new KeyNotFoundException($"Deck {request.DeckId} not found.");

        var card = await _cardRepository.GetByIdAsync(request.CardId, cancellationToken)
            ?? throw new KeyNotFoundException($"Card {request.CardId} not found.");

        deck.AddCard(card, request.Quantity, request.Category, _timeProvider.GetUtcNow().UtcDateTime);
        await _deckRepository.UpdateAsync(deck, cancellationToken);

        return deck;
    }
}
