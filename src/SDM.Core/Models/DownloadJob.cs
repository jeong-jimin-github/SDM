namespace SDM.Core.Models;

public sealed class SegmentState
{
    public int Index { get; set; }
    public long Start { get; set; }
    public long End { get; set; }
    public long Written { get; set; }

    public long Length => End - Start + 1;
    public bool IsComplete => Written >= Length;
    public long AbsolutePosition => Start + Written;
    public long Remaining => Math.Max(0, Length - Written);
}

public sealed class DownloadJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Url { get; set; } = "";
    public string FileName { get; set; } = "";
    public string SaveDirectory { get; set; } = "";
    public string? Referrer { get; set; }
    public string? Cookies { get; set; }
    public string? UserAgent { get; set; }
    public string? Mime { get; set; }
    public Dictionary<string, string> Headers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public long TotalBytes { get; set; }
    public long DownloadedBytes { get; set; }
    public int Connections { get; set; } = 8;
    public bool SupportsRanges { get; set; }
    public DownloadStatus Status { get; set; } = DownloadStatus.Queued;
    public string Category { get; set; } = "general";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? ScheduledAt { get; set; }
    public string? ErrorMessage { get; set; }
    public List<SegmentState> Segments { get; set; } = [];
    public string? FinalUrl { get; set; }
    public bool OpenFolderWhenDone { get; set; }

    public string SavePath => Path.Combine(SaveDirectory, FileName);
    public string TempPath => SavePath + ".sdmpart";
    public double Progress => TotalBytes > 0 ? Math.Clamp((double)DownloadedBytes / TotalBytes, 0, 1) : 0;
}
