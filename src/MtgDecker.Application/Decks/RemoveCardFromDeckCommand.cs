using FluentValidation;
using MediatR;
using MtgDecker.Application.Interfaces;
using MtgDecker.Domain.Entities;
using MtgDecker.Domain.Enums;

namespace MtgDecker.Application.Decks;

public record RemoveCardFromDeckCommand(Guid DeckId, Guid CardId, DeckCategory Category) : IRequest<Deck>;

public class RemoveCardFromDeckValidator : AbstractValidator<RemoveCardFromDeckCommand>
{
    public RemoveCardFromDeckValidator()
    {
        RuleFor(x => x.DeckId).NotEmpty();
        RuleFor(x => x.CardId).NotEmpty();
        RuleFor(x => x.Category).IsInEnum();
    }
}

public class RemoveCardFromDeckHandler : IRequestHandler<RemoveCardFromDeckCommand, Deck>
{
    private readonly IDeckRepository _deckRepository;
    private readonly TimeProvider _timeProvider;

    public RemoveCardFromDeckHandler(IDeckRepository deckRepository, TimeProvider timeProvider)
    {
        _deckRepository = deckRepository;
        _timeProvider = timeProvider;
    }

    public async Task<Deck> Handle(RemoveCardFromDeckCommand request, CancellationToken cancellationToken)
    {
        var deck = await _deckRepository.GetByIdAsync(request.DeckId, cancellationToken)
            ?? throw new KeyNotFoundException($"Deck {request.DeckId} not found.");

        if (deck.IsSystemDeck)
            throw new InvalidOperationException("System decks cannot be modified.");

        deck.RemoveCard(request.CardId, request.Category, _timeProvider.GetUtcNow().UtcDateTime);
        await _deckRepository.UpdateAsync(deck, cancellationToken);

        return deck;
    }
}
