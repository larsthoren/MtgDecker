using MediatR;
using MtgDecker.Application.Interfaces;

namespace MtgDecker.Application.Collection;

public record RemoveFromCollectionCommand(Guid Id) : IRequest;

public class RemoveFromCollectionHandler : IRequestHandler<RemoveFromCollectionCommand>
{
    private readonly ICollectionRepository _collectionRepository;

    public RemoveFromCollectionHandler(ICollectionRepository collectionRepository)
    {
        _collectionRepository = collectionRepository;
    }

    public async Task Handle(RemoveFromCollectionCommand request, CancellationToken cancellationToken)
    {
        await _collectionRepository.DeleteAsync(request.Id, cancellationToken);
    }
}
