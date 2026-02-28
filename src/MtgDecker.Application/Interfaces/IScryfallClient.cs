using MtgDecker.Domain.Entities;

namespace MtgDecker.Application.Interfaces;

public interface IScryfallClient
{
    Task<BulkDataInfo?> GetBulkDataInfoAsync(string dataType = "default_cards", CancellationToken ct = default);
    Task<Stream> DownloadBulkDataAsync(string downloadUri, CancellationToken ct = default);
    Task<(List<Card> Found, List<string> NotFound)> FetchCardsByNamesAsync(
        IEnumerable<string> names, CancellationToken ct = default);
}
