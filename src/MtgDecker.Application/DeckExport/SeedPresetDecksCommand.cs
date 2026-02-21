using MediatR;
using MtgDecker.Application.Interfaces;

namespace MtgDecker.Application.DeckExport;

public record SeedPresetDecksCommand() : IRequest<SeedPresetDecksResult>;

public record SeedPresetDecksResult(
    List<string> Created,
    List<string> Skipped,
    Dictionary<string, List<string>> Unresolved);

public class SeedPresetDecksHandler : IRequestHandler<SeedPresetDecksCommand, SeedPresetDecksResult>
{
    private readonly IMediator _mediator;
    private readonly IDeckRepository _deckRepository;

    public SeedPresetDecksHandler(IMediator mediator, IDeckRepository deckRepository)
    {
        _mediator = mediator;
        _deckRepository = deckRepository;
    }

    public async Task<SeedPresetDecksResult> Handle(
        SeedPresetDecksCommand request, CancellationToken cancellationToken)
    {
        var existingDecks = await _deckRepository.ListSystemDecksAsync(cancellationToken);
        var existingNames = existingDecks.Select(d => d.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var created = new List<string>();
        var skipped = new List<string>();
        var unresolved = new Dictionary<string, List<string>>();

        foreach (var preset in PresetDeckRegistry.All)
        {
            if (existingNames.Contains(preset.Name))
            {
                skipped.Add(preset.Name);
                continue;
            }

            var result = await _mediator.Send(
                new ImportDeckCommand(preset.DeckTextMtgo, "MTGO", preset.Name, preset.Format, null),
                cancellationToken);

            created.Add(preset.Name);
            if (result.UnresolvedCards.Count > 0)
                unresolved[preset.Name] = result.UnresolvedCards;
        }

        return new SeedPresetDecksResult(created, skipped, unresolved);
    }
}
