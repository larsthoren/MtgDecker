using FluentValidation;
using MediatR;
using MtgDecker.Application.Interfaces;
using MtgDecker.Domain.Entities;
using MtgDecker.Domain.Enums;

namespace MtgDecker.Application.Decks;

public record CloneDeckCommand(Guid SourceDeckId, Guid UserId) : IRequest<Deck>;

public class CloneDeckCommandValidator : AbstractValidator<CloneDeckCommand>
{
    public CloneDeckCommandValidator()
    {
        RuleFor(x => x.SourceDeckId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
    }
}

public class CloneDeckCommandHandler : IRequestHandler<CloneDeckCommand, Deck>
{
    private readonly IDeckRepository _deckRepository;
    private readonly TimeProvider _timeProvider;

    public CloneDeckCommandHandler(IDeckRepository deckRepository, TimeProvider timeProvider)
    {
        _deckRepository = deckRepository;
        _timeProvider = timeProvider;
    }

    public async Task<Deck> Handle(CloneDeckCommand request, CancellationToken ct)
    {
        var source = await _deckRepository.GetByIdAsync(request.SourceDeckId, ct)
            ?? throw new KeyNotFoundException($"Deck {request.SourceDeckId} not found.");

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;

        var clone = new Deck
        {
            Name = source.Name,
            Format = source.Format,
            Description = source.Description,
            UserId = request.UserId,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
            Entries = source.Entries.Select(e => new DeckEntry
            {
                CardId = e.CardId,
                Quantity = e.Quantity,
                Category = e.Category
            }).ToList()
        };

        await _deckRepository.AddAsync(clone, ct);
        return clone;
    }
}
