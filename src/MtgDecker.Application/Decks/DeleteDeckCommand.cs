using MediatR;
using MtgDecker.Application.Interfaces;

namespace MtgDecker.Application.Decks;

public record DeleteDeckCommand(Guid Id) : IRequest;

public class DeleteDeckHandler : IRequestHandler<DeleteDeckCommand>
{
    private readonly IDeckRepository _deckRepository;

    public DeleteDeckHandler(IDeckRepository deckRepository)
    {
        _deckRepository = deckRepository;
    }

    public async Task Handle(DeleteDeckCommand request, CancellationToken cancellationToken)
    {
        await _deckRepository.DeleteAsync(request.Id, cancellationToken);
    }
}
