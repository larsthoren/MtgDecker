using FluentValidation;
using MediatR;
using MtgDecker.Application.Interfaces;
using MtgDecker.Domain.Entities;
using MtgDecker.Domain.Enums;

namespace MtgDecker.Application.Decks;

public record UpdateDeckFormatCommand(Guid DeckId, Format Format) : IRequest<Deck>;

public class UpdateDeckFormatValidator : AbstractValidator<UpdateDeckFormatCommand>
{
    public UpdateDeckFormatValidator()
    {
        RuleFor(x => x.DeckId).NotEmpty();
        RuleFor(x => x.Format).IsInEnum();
    }
}

public class UpdateDeckFormatHandler : IRequestHandler<UpdateDeckFormatCommand, Deck>
{
    private readonly IDeckRepository _deckRepository;
    private readonly TimeProvider _timeProvider;

    public UpdateDeckFormatHandler(IDeckRepository deckRepository, TimeProvider timeProvider)
    {
        _deckRepository = deckRepository;
        _timeProvider = timeProvider;
    }

    public async Task<Deck> Handle(UpdateDeckFormatCommand request, CancellationToken cancellationToken)
    {
        var deck = await _deckRepository.GetByIdAsync(request.DeckId, cancellationToken)
            ?? throw new KeyNotFoundException($"Deck {request.DeckId} not found.");

        deck.Format = request.Format;
        deck.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
        await _deckRepository.UpdateAsync(deck, cancellationToken);

        return deck;
    }
}
