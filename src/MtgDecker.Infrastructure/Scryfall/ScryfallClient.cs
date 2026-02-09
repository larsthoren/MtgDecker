using System.Text.Json;
using MtgDecker.Application.Interfaces;

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
