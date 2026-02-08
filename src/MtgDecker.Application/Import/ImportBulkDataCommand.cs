using MediatR;
using MtgDecker.Application.Interfaces;

namespace MtgDecker.Application.Import;

public record ImportBulkDataCommand(IProgress<int>? Progress = null) : IRequest<int>;

public class ImportBulkDataHandler : IRequestHandler<ImportBulkDataCommand, int>
{
    private readonly IScryfallClient _scryfallClient;
    private readonly IBulkDataImporter _bulkDataImporter;

    public ImportBulkDataHandler(IScryfallClient scryfallClient, IBulkDataImporter bulkDataImporter)
    {
        _scryfallClient = scryfallClient;
        _bulkDataImporter = bulkDataImporter;
    }

    public async Task<int> Handle(ImportBulkDataCommand request, CancellationToken cancellationToken)
    {
        var info = await _scryfallClient.GetBulkDataInfoAsync("default_cards", cancellationToken)
            ?? throw new InvalidOperationException("Could not retrieve bulk data info from Scryfall.");

        await using var stream = await _scryfallClient.DownloadBulkDataAsync(info.DownloadUri, cancellationToken);

        return await _bulkDataImporter.ImportFromStreamAsync(stream, request.Progress, cancellationToken);
    }
}
