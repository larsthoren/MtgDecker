using FluentValidation;
using MediatR;
using MtgDecker.Application.Interfaces;
using MtgDecker.Domain.Entities;

namespace MtgDecker.Application.Collection;

public record SearchCollectionQuery(Guid UserId, string? SearchText = null) : IRequest<List<CollectionEntry>>;

public class SearchCollectionValidator : AbstractValidator<SearchCollectionQuery>
{
    public SearchCollectionValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
    }
}

public class SearchCollectionHandler : IRequestHandler<SearchCollectionQuery, List<CollectionEntry>>
{
    private readonly ICollectionRepository _collectionRepository;

    public SearchCollectionHandler(ICollectionRepository collectionRepository)
    {
        _collectionRepository = collectionRepository;
    }

    public async Task<List<CollectionEntry>> Handle(SearchCollectionQuery request, CancellationToken cancellationToken)
    {
        return await _collectionRepository.SearchAsync(request.UserId, request.SearchText, cancellationToken);
    }
}
