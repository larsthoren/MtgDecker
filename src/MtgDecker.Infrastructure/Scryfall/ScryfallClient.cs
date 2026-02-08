using System.Text.Json;
using MtgDecker.Application.Interfaces;

namespace MtgDecker.Infrastructure.Scryfall;

public class ScryfallClient : IScryfallClient
{
    private readonly HttpClient _httpClient;
    private const string BulkDataUrl = "https://api.scryfall.com/bulk-data";

    public ScryfallClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("MtgDecker/1.0");
    }

    public async Task<BulkDataInfo?> GetBulkDataInfoAsync(string dataType = "default_cards", CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync(BulkDataUrl, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStreamAsync(ct);
        var bulkData = await JsonSerializer.DeserializeAsync<ScryfallBulkDataResponse>(json, cancellationToken: ct);

        var entry = bulkData?.Data.FirstOrDefault(d => d.Type == dataType);
        if (entry == null) return null;

        return new BulkDataInfo
        {
            DownloadUri = entry.DownloadUri,
            UpdatedAt = entry.UpdatedAt,
            Size = entry.Size
        };
    }

    public async Task<Stream> DownloadBulkDataAsync(string downloadUri, CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync(downloadUri, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStreamAsync(ct);
    }
}
