using MediatR;
using MtgDecker.Application.Interfaces;
using MtgDecker.Domain.Entities;

namespace MtgDecker.Application.Cards;

public record GetCardByNameQuery(string Name) : IRequest<Card?>;

public class GetCardByNameHandler : IRequestHandler<GetCardByNameQuery, Card?>
{
    private readonly ICardRepository _cardRepository;

    public GetCardByNameHandler(ICardRepository cardRepository)
    {
        _cardRepository = cardRepository;
    }

    public async Task<Card?> Handle(GetCardByNameQuery request, CancellationToken cancellationToken)
    {
        return await _cardRepository.GetByNameAsync(request.Name, cancellationToken);
    }
}
