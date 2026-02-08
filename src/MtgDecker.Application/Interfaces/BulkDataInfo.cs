namespace MtgDecker.Application.Interfaces;

public class BulkDataInfo
{
    public string DownloadUri { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
    public long Size { get; set; }
}
