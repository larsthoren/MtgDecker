using MediatR;
using MtgDecker.Application.Interfaces;

namespace MtgDecker.Application.Cards;

public record SearchSetNamesQuery(string SearchText) : IRequest<List<SetInfo>>;

public record SetInfo(string SetCode, string SetName);

public class SearchSetNamesHandler : IRequestHandler<SearchSetNamesQuery, List<SetInfo>>
{
    private readonly ICardRepository _cardRepository;

    public SearchSetNamesHandler(ICardRepository cardRepository)
    {
        _cardRepository = cardRepository;
    }

    public async Task<List<SetInfo>> Handle(SearchSetNamesQuery request, CancellationToken cancellationToken)
    {
        return await _cardRepository.GetDistinctSetsAsync(request.SearchText, cancellationToken);
    }
}

public record SearchTypeNamesQuery(string SearchText) : IRequest<List<string>>;

public class SearchTypeNamesHandler : IRequestHandler<SearchTypeNamesQuery, List<string>>
{
    private readonly ICardRepository _cardRepository;

    public SearchTypeNamesHandler(ICardRepository cardRepository)
    {
        _cardRepository = cardRepository;
    }

    public async Task<List<string>> Handle(SearchTypeNamesQuery request, CancellationToken cancellationToken)
    {
        return await _cardRepository.GetDistinctTypesAsync(request.SearchText, cancellationToken);
    }
}
