using MediatR;
using MtgDecker.Application.Interfaces;
using MtgDecker.Domain.Entities;

namespace MtgDecker.Application.Decks;

public record ListSystemDecksQuery() : IRequest<List<Deck>>;

public class ListSystemDecksQueryHandler : IRequestHandler<ListSystemDecksQuery, List<Deck>>
{
    private readonly IDeckRepository _deckRepository;

    public ListSystemDecksQueryHandler(IDeckRepository deckRepository)
    {
        _deckRepository = deckRepository;
    }

    public async Task<List<Deck>> Handle(ListSystemDecksQuery request, CancellationToken cancellationToken)
    {
        return await _deckRepository.ListSystemDecksAsync(cancellationToken);
    }
}
