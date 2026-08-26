namespace SDM.Core.Models;

public enum DownloadStatus
{
    Queued,
    Connecting,
    Downloading,
    Paused,
    Completed,
    Failed,
    Canceled,
    Scheduled
}
