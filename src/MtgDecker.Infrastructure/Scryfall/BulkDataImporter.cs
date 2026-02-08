using System.Text.Json;
using MtgDecker.Application.Interfaces;

namespace MtgDecker.Infrastructure.Scryfall;

public class BulkDataImporter : IBulkDataImporter
{
    private readonly ICardRepository _cardRepository;
    private const int BatchSize = 1000;

    public BulkDataImporter(ICardRepository cardRepository)
    {
        _cardRepository = cardRepository;
    }

    public async Task<int> ImportFromStreamAsync(Stream jsonStream, IProgress<int>? progress = null, CancellationToken ct = default)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var batch = new List<Domain.Entities.Card>();
        int totalImported = 0;

        await foreach (var scryfallCard in JsonSerializer.DeserializeAsyncEnumerable<ScryfallCard>(jsonStream, options, ct))
        {
            if (scryfallCard == null) continue;

            var card = ScryfallCardMapper.MapToCard(scryfallCard);
            batch.Add(card);

            if (batch.Count >= BatchSize)
            {
                await _cardRepository.UpsertBatchAsync(batch, ct);
                totalImported += batch.Count;
                progress?.Report(totalImported);
                batch.Clear();
            }
        }

        if (batch.Count > 0)
        {
            await _cardRepository.UpsertBatchAsync(batch, ct);
            totalImported += batch.Count;
            progress?.Report(totalImported);
        }

        return totalImported;
    }
}
