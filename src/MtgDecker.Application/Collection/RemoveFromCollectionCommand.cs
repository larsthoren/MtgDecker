using FluentValidation;
using MediatR;
using MtgDecker.Application.Interfaces;

namespace MtgDecker.Application.Collection;

public record RemoveFromCollectionCommand(Guid Id) : IRequest;

public class RemoveFromCollectionValidator : AbstractValidator<RemoveFromCollectionCommand>
{
    public RemoveFromCollectionValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class RemoveFromCollectionHandler : IRequestHandler<RemoveFromCollectionCommand>
{
    private readonly ICollectionRepository _collectionRepository;

    public RemoveFromCollectionHandler(ICollectionRepository collectionRepository)
    {
        _collectionRepository = collectionRepository;
    }

    public async Task Handle(RemoveFromCollectionCommand request, CancellationToken cancellationToken)
    {
        var entry = await _collectionRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Collection entry {request.Id} not found.");

        await _collectionRepository.DeleteAsync(request.Id, cancellationToken);
    }
}
