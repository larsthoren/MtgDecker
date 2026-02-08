using MediatR;
using MtgDecker.Application.Interfaces;

namespace MtgDecker.Application.Import;

public record CheckForUpdatesQuery : IRequest<BulkDataInfo?>;

public class CheckForUpdatesHandler : IRequestHandler<CheckForUpdatesQuery, BulkDataInfo?>
{
    private readonly IScryfallClient _scryfallClient;

    public CheckForUpdatesHandler(IScryfallClient scryfallClient)
    {
        _scryfallClient = scryfallClient;
    }

    public async Task<BulkDataInfo?> Handle(CheckForUpdatesQuery request, CancellationToken cancellationToken)
    {
        return await _scryfallClient.GetBulkDataInfoAsync("default_cards", cancellationToken);
    }
}
