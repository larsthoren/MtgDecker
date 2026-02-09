using FluentValidation;
using MediatR;
using MtgDecker.Application.Interfaces;
using MtgDecker.Domain.Entities;

namespace MtgDecker.Application.Decks;

public record GetDeckQuery(Guid Id) : IRequest<Deck?>;

public class GetDeckValidator : AbstractValidator<GetDeckQuery>
{
    public GetDeckValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class GetDeckHandler : IRequestHandler<GetDeckQuery, Deck?>
{
    private readonly IDeckRepository _deckRepository;

    public GetDeckHandler(IDeckRepository deckRepository)
    {
        _deckRepository = deckRepository;
    }

    public async Task<Deck?> Handle(GetDeckQuery request, CancellationToken cancellationToken)
    {
        return await _deckRepository.GetByIdAsync(request.Id, cancellationToken);
    }
}
