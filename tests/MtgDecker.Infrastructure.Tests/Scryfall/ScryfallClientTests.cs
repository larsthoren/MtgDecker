using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using MtgDecker.Infrastructure.Scryfall;

namespace MtgDecker.Infrastructure.Tests.Scryfall;

public class ScryfallClientTests
{
    [Fact]
    public async Task GetBulkDataInfoAsync_ReturnsCorrectEntry()
    {
        var bulkDataResponse = new ScryfallBulkDataResponse
        {
            Data = new List<ScryfallBulkDataEntry>
            {
                new() { Type = "oracle_cards", DownloadUri = "https://data.scryfall.io/oracle.json", UpdatedAt = DateTime.UtcNow, Size = 50_000_000 },
                new() { Type = "default_cards", DownloadUri = "https://data.scryfall.io/default.json", UpdatedAt = new DateTime(2026, 2, 8, 12, 0, 0, DateTimeKind.Utc), Size = 80_000_000 }
            }
        };

        var handler = new FakeHttpMessageHandler(JsonSerializer.Serialize(bulkDataResponse));
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.scryfall.com") };
        var client = new ScryfallClient(httpClient);

        var result = await client.GetBulkDataInfoAsync("default_cards");

        result.Should().NotBeNull();
        result!.DownloadUri.Should().Be("https://data.scryfall.io/default.json");
        result.Size.Should().Be(80_000_000);
    }

    [Fact]
    public async Task GetBulkDataInfoAsync_UnknownType_ReturnsNull()
    {
        var bulkDataResponse = new ScryfallBulkDataResponse
        {
            Data = new List<ScryfallBulkDataEntry>
            {
                new() { Type = "default_cards", DownloadUri = "https://data.scryfall.io/default.json", Size = 80_000_000 }
            }
        };

        var handler = new FakeHttpMessageHandler(JsonSerializer.Serialize(bulkDataResponse));
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.scryfall.com") };
        var client = new ScryfallClient(httpClient);

        var result = await client.GetBulkDataInfoAsync("nonexistent_type");

        result.Should().BeNull();
    }

    [Fact]
    public async Task DownloadBulkDataAsync_ReturnsStream()
    {
        var cardJson = "[{\"id\":\"abc\",\"name\":\"Test\"}]";
        var handler = new FakeHttpMessageHandler(cardJson);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://data.scryfall.io") };
        var client = new ScryfallClient(httpClient);

        var stream = await client.DownloadBulkDataAsync("https://data.scryfall.io/default.json");

        stream.Should().NotBeNull();
        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();
        content.Should().Contain("Test");
    }

    private class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _responseContent;

        public FakeHttpMessageHandler(string responseContent)
        {
            _responseContent = responseContent;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responseContent, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }
}
