namespace MtgDecker.Application.Interfaces;

public interface IScryfallClient
{
    Task<BulkDataInfo?> GetBulkDataInfoAsync(string dataType = "default_cards", CancellationToken ct = default);
    Task<Stream> DownloadBulkDataAsync(string downloadUri, CancellationToken ct = default);
}
