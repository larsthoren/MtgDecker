using FluentValidation;
using MediatR;
using MtgDecker.Application.Interfaces;
using MtgDecker.Domain.Entities;
using MtgDecker.Domain.Enums;

namespace MtgDecker.Application.Collection;

public record AddToCollectionCommand(
    Guid UserId,
    Guid CardId,
    int Quantity,
    bool IsFoil,
    CardCondition Condition) : IRequest<CollectionEntry>;

public class AddToCollectionValidator : AbstractValidator<AddToCollectionCommand>
{
    public AddToCollectionValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.CardId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThanOrEqualTo(1);
        RuleFor(x => x.Condition).IsInEnum();
    }
}

public class AddToCollectionHandler : IRequestHandler<AddToCollectionCommand, CollectionEntry>
{
    private readonly ICollectionRepository _collectionRepository;
    private readonly ICardRepository _cardRepository;

    public AddToCollectionHandler(ICollectionRepository collectionRepository, ICardRepository cardRepository)
    {
        _collectionRepository = collectionRepository;
        _cardRepository = cardRepository;
    }

    public async Task<CollectionEntry> Handle(AddToCollectionCommand request, CancellationToken cancellationToken)
    {
        var card = await _cardRepository.GetByIdAsync(request.CardId, cancellationToken)
            ?? throw new KeyNotFoundException($"Card {request.CardId} not found.");

        var entry = new CollectionEntry
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            CardId = request.CardId,
            Quantity = request.Quantity,
            IsFoil = request.IsFoil,
            Condition = request.Condition
        };

        await _collectionRepository.AddAsync(entry, cancellationToken);
        return entry;
    }
}
