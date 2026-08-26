namespace SDM.Core.Models;

public sealed class DownloadProgress
{
    public Guid JobId { get; init; }
    public DownloadStatus Status { get; init; }
    public long DownloadedBytes { get; init; }
    public long TotalBytes { get; init; }
    public long BytesPerSecond { get; init; }
    public TimeSpan? Eta { get; init; }
    public int ActiveConnections { get; init; }
    public string? ErrorMessage { get; init; }
    public string? FileName { get; init; }
}
