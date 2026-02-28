using FluentValidation;
using MediatR;
using MtgDecker.Application.Interfaces;

namespace MtgDecker.Application.Decks;

public record DeleteDeckCommand(Guid Id) : IRequest;

public class DeleteDeckValidator : AbstractValidator<DeleteDeckCommand>
{
    public DeleteDeckValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class DeleteDeckHandler : IRequestHandler<DeleteDeckCommand>
{
    private readonly IDeckRepository _deckRepository;

    public DeleteDeckHandler(IDeckRepository deckRepository)
    {
        _deckRepository = deckRepository;
    }

    public async Task Handle(DeleteDeckCommand request, CancellationToken cancellationToken)
    {
        await _deckRepository.GetMutableDeckAsync(request.Id, cancellationToken);

        await _deckRepository.DeleteAsync(request.Id, cancellationToken);
    }
}
