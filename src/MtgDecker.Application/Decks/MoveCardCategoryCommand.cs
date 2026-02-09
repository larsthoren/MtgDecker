using FluentValidation;
using MediatR;
using MtgDecker.Application.Interfaces;
using MtgDecker.Domain.Entities;
using MtgDecker.Domain.Enums;

namespace MtgDecker.Application.Decks;

public record MoveCardCategoryCommand(Guid DeckId, Guid CardId, DeckCategory From, DeckCategory To) : IRequest<Deck>;

public class MoveCardCategoryValidator : AbstractValidator<MoveCardCategoryCommand>
{
    public MoveCardCategoryValidator()
    {
        RuleFor(x => x.DeckId).NotEmpty();
        RuleFor(x => x.CardId).NotEmpty();
        RuleFor(x => x.From).IsInEnum();
        RuleFor(x => x.To).IsInEnum();
        RuleFor(x => x).Must(x => x.From != x.To)
            .WithMessage("Source and target categories must be different.");
    }
}

public class MoveCardCategoryHandler : IRequestHandler<MoveCardCategoryCommand, Deck>
{
    private readonly IDeckRepository _deckRepository;
    private readonly ICardRepository _cardRepository;
    private readonly TimeProvider _timeProvider;

    public MoveCardCategoryHandler(IDeckRepository deckRepository, ICardRepository cardRepository, TimeProvider timeProvider)
    {
        _deckRepository = deckRepository;
        _cardRepository = cardRepository;
        _timeProvider = timeProvider;
    }

    public async Task<Deck> Handle(MoveCardCategoryCommand request, CancellationToken cancellationToken)
    {
        var deck = await _deckRepository.GetByIdAsync(request.DeckId, cancellationToken)
            ?? throw new KeyNotFoundException($"Deck {request.DeckId} not found.");

        var card = await _cardRepository.GetByIdAsync(request.CardId, cancellationToken)
            ?? throw new KeyNotFoundException($"Card {request.CardId} not found.");

        deck.MoveCardCategory(card, request.From, request.To, _timeProvider.GetUtcNow().UtcDateTime);
        await _deckRepository.UpdateAsync(deck, cancellationToken);

        return deck;
    }
}
