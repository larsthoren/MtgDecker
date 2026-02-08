using MediatR;
using MtgDecker.Application.Interfaces;
using MtgDecker.Domain.Entities;
using MtgDecker.Domain.Enums;

namespace MtgDecker.Application.Collection;

public record UpdateCollectionEntryCommand(Guid Id, int Quantity, bool IsFoil, CardCondition Condition) : IRequest<CollectionEntry>;

public class UpdateCollectionEntryHandler : IRequestHandler<UpdateCollectionEntryCommand, CollectionEntry>
{
    private readonly ICollectionRepository _collectionRepository;

    public UpdateCollectionEntryHandler(ICollectionRepository collectionRepository)
    {
        _collectionRepository = collectionRepository;
    }

    public async Task<CollectionEntry> Handle(UpdateCollectionEntryCommand request, CancellationToken cancellationToken)
    {
        var entry = await _collectionRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Collection entry {request.Id} not found.");

        entry.Quantity = request.Quantity;
        entry.IsFoil = request.IsFoil;
        entry.Condition = request.Condition;

        await _collectionRepository.UpdateAsync(entry, cancellationToken);
        return entry;
    }
}
