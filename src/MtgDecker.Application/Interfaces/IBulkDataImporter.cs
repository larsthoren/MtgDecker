namespace MtgDecker.Application.Interfaces;

public interface IBulkDataImporter
{
    Task<int> ImportFromStreamAsync(Stream jsonStream, IProgress<int>? progress = null, CancellationToken ct = default);
}
