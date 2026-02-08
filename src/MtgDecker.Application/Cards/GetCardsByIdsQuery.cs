using MediatR;
using MtgDecker.Application.Interfaces;
using MtgDecker.Domain.Entities;

namespace MtgDecker.Application.Cards;

public record GetCardsByIdsQuery(List<Guid> CardIds) : IRequest<List<Card>>;

public class GetCardsByIdsHandler : IRequestHandler<GetCardsByIdsQuery, List<Card>>
{
    private readonly ICardRepository _cardRepository;

    public GetCardsByIdsHandler(ICardRepository cardRepository)
    {
        _cardRepository = cardRepository;
    }

    public async Task<List<Card>> Handle(GetCardsByIdsQuery request, CancellationToken cancellationToken)
    {
        return await _cardRepository.GetByIdsAsync(request.CardIds, cancellationToken);
    }
}
