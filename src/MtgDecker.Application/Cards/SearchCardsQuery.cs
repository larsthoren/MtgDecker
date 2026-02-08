using MediatR;
using MtgDecker.Application.Interfaces;
using MtgDecker.Domain.Entities;

namespace MtgDecker.Application.Cards;

public record SearchCardsQuery(CardSearchFilter Filter) : IRequest<SearchCardsResult>;

public record SearchCardsResult(List<Card> Cards, int TotalCount);

public class SearchCardsHandler : IRequestHandler<SearchCardsQuery, SearchCardsResult>
{
    private readonly ICardRepository _cardRepository;

    public SearchCardsHandler(ICardRepository cardRepository)
    {
        _cardRepository = cardRepository;
    }

    public async Task<SearchCardsResult> Handle(SearchCardsQuery request, CancellationToken cancellationToken)
    {
        var (cards, totalCount) = await _cardRepository.SearchAsync(request.Filter, cancellationToken);
        return new SearchCardsResult(cards, totalCount);
    }
}
