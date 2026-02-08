namespace MtgDecker.Domain.Entities;

public class BulkDataImportMetadata
{
    public Guid Id { get; set; }
    public DateTime ImportedAt { get; set; }
    public string ScryfallDataType { get; set; } = string.Empty;
    public int CardCount { get; set; }
}
