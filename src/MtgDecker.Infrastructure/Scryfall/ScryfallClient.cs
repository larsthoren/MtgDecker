using System.Text;
using System.Text.Json;
using MtgDecker.Application.Interfaces;
using MtgDecker.Domain.Entities;

namespace MtgDecker.Infrastructure.Scryfall;

public class ScryfallClient : IScryfallClient
{
    private readonly HttpClient _httpClient;
    private const string BulkDataUrl = "bulk-data";

    public ScryfallClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
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
        var stream = await response.Content.ReadAsStreamAsync(ct);
        return new ResponseStream(stream, response);
    }

    public async Task<(List<Card> Found, List<string> NotFound)> FetchCardsByNamesAsync(
        IEnumerable<string> names, CancellationToken ct = default)
    {
        var nameList = names.Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
        var allCards = new List<Card>();
        var allNotFound = new List<string>();

        // Scryfall collection endpoint accepts max 75 identifiers per request
        var batches = nameList.Chunk(75).ToList();
        for (var i = 0; i < batches.Count; i++)
        {
            var batch = batches[i];
            var identifiers = batch.Select(n => new { name = n }).ToArray();
            var requestBody = new { identifiers };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, System.Net.Http.Headers.MediaTypeHeaderValue.Parse("application/json"));

            var response = await _httpClient.PostAsync("cards/collection", content, ct);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                throw new HttpRequestException(
                    $"Scryfall collection API returned {(int)response.StatusCode}: {errorBody}");
            }

            var responseStream = await response.Content.ReadAsStreamAsync(ct);
            var result = await JsonSerializer.DeserializeAsync<ScryfallCollectionResponse>(
                responseStream, cancellationToken: ct);

            if (result?.Data != null)
                allCards.AddRange(result.Data.Select(ScryfallCardMapper.MapToCard));

            if (result?.NotFound != null)
                allNotFound.AddRange(result.NotFound.Where(nf => nf.Name != null).Select(nf => nf.Name!));

            // Respect Scryfall rate limit (100ms between requests)
            if (i < batches.Count - 1)
                await Task.Delay(100, ct);
        }

        return (allCards, allNotFound);
    }

    private sealed class ResponseStream : Stream
    {
        private readonly Stream _inner;
        private readonly HttpResponseMessage _response;

        public ResponseStream(Stream inner, HttpResponseMessage response)
        {
            _inner = inner;
            _response = response;
        }

        // Delegate all Stream members to _inner
        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => _inner.CanWrite;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => _inner.Position = value; }
        public override void Flush() => _inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void SetLength(long value) => _inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => _inner.ReadAsync(buffer, cancellationToken);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
                _response.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
