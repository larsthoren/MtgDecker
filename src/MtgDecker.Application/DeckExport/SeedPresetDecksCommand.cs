using MediatR;
using MtgDecker.Application.Decks;

namespace MtgDecker.Application.DeckExport;

public record SeedPresetDecksCommand(Guid UserId) : IRequest<SeedPresetDecksResult>;

public record SeedPresetDecksResult(
    List<string> Created,
    List<string> Skipped,
    Dictionary<string, List<string>> Unresolved);

public class SeedPresetDecksHandler : IRequestHandler<SeedPresetDecksCommand, SeedPresetDecksResult>
{
    private readonly IMediator _mediator;

    public SeedPresetDecksHandler(IMediator mediator) => _mediator = mediator;

    public async Task<SeedPresetDecksResult> Handle(
        SeedPresetDecksCommand request, CancellationToken cancellationToken)
    {
        var existing = await _mediator.Send(new ListDecksQuery(request.UserId), cancellationToken);
        var existingNames = existing.Select(d => d.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

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
                new ImportDeckCommand(preset.DeckTextMtgo, "MTGO", preset.Name, preset.Format, request.UserId),
                cancellationToken);

            created.Add(preset.Name);
            if (result.UnresolvedCards.Count > 0)
                unresolved[preset.Name] = result.UnresolvedCards;
        }

        return new SeedPresetDecksResult(created, skipped, unresolved);
    }
}
