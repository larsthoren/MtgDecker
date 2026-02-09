using FluentValidation;
using MediatR;
using MtgDecker.Application.Interfaces;
using MtgDecker.Domain.Entities;

namespace MtgDecker.Application.Cards;

public record GetCardByIdQuery(Guid Id) : IRequest<Card?>;

public class GetCardByIdValidator : AbstractValidator<GetCardByIdQuery>
{
    public GetCardByIdValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class GetCardByIdHandler : IRequestHandler<GetCardByIdQuery, Card?>
{
    private readonly ICardRepository _cardRepository;

    public GetCardByIdHandler(ICardRepository cardRepository)
    {
        _cardRepository = cardRepository;
    }

    public async Task<Card?> Handle(GetCardByIdQuery request, CancellationToken cancellationToken)
    {
        return await _cardRepository.GetByIdAsync(request.Id, cancellationToken);
    }
}
