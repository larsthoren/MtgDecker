using FluentValidation;
using MediatR;
using MtgDecker.Application.Interfaces;
using MtgDecker.Domain.Entities;

namespace MtgDecker.Application.Decks;

public record ListDecksQuery(Guid UserId) : IRequest<List<Deck>>;

public class ListDecksValidator : AbstractValidator<ListDecksQuery>
{
    public ListDecksValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
    }
}

public class ListDecksHandler : IRequestHandler<ListDecksQuery, List<Deck>>
{
    private readonly IDeckRepository _deckRepository;

    public ListDecksHandler(IDeckRepository deckRepository)
    {
        _deckRepository = deckRepository;
    }

    public async Task<List<Deck>> Handle(ListDecksQuery request, CancellationToken cancellationToken)
    {
        return await _deckRepository.ListByUserAsync(request.UserId, cancellationToken);
    }
}
