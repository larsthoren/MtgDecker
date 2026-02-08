using FluentValidation;
using MediatR;
using MtgDecker.Application.Interfaces;
using MtgDecker.Domain.Entities;
using MtgDecker.Domain.Enums;

namespace MtgDecker.Application.Decks;

public record CreateDeckCommand(string Name, Format Format, string? Description, Guid UserId) : IRequest<Deck>;

public class CreateDeckValidator : AbstractValidator<CreateDeckCommand>
{
    public CreateDeckValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Format).IsInEnum();
        RuleFor(x => x.UserId).NotEmpty();
    }
}

public class CreateDeckHandler : IRequestHandler<CreateDeckCommand, Deck>
{
    private readonly IDeckRepository _deckRepository;

    public CreateDeckHandler(IDeckRepository deckRepository)
    {
        _deckRepository = deckRepository;
    }

    public async Task<Deck> Handle(CreateDeckCommand request, CancellationToken cancellationToken)
    {
        var deck = new Deck
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Format = request.Format,
            Description = request.Description,
            UserId = request.UserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _deckRepository.AddAsync(deck, cancellationToken);
        return deck;
    }
}
