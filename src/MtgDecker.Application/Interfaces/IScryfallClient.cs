namespace MtgDecker.Application.Interfaces;

public interface IScryfallClient
{
    Task<BulkDataInfo?> GetBulkDataInfoAsync(string dataType = "default_cards", CancellationToken ct = default);
    Task<Stream> DownloadBulkDataAsync(string downloadUri, CancellationToken ct = default);
}

public class BulkDataInfo
{
    public string DownloadUri { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
    public long Size { get; set; }
}
