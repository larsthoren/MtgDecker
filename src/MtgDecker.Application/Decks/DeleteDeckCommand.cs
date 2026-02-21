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
        var deck = await _deckRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Deck {request.Id} not found.");

        if (deck.IsSystemDeck)
            throw new InvalidOperationException("System decks cannot be modified.");

        await _deckRepository.DeleteAsync(request.Id, cancellationToken);
    }
}
