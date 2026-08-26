using SDM.Core.Engine;
using SDM.Core.Models;

namespace SDM.App.ViewModels;

public sealed class DownloadItemViewModel : ObservableObject
{
    private string _fileName = "";
    private string _url = "";
    private string _statusText = "";
    private string _sizeText = "";
    private string _speedText = "—";
    private string _etaText = "—";
    private string _detail = "";
    private string _error = "";
    private double _progress;
    private DownloadStatus _status;
    private string _savePath = "";
    private string _category = "general";
    private long _speed;

    public Guid Id { get; private set; }

    public string FileName { get => _fileName; set => SetProperty(ref _fileName, value); }
    public string Url { get => _url; set => SetProperty(ref _url, value); }
    public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value); }
    public string SizeText { get => _sizeText; set => SetProperty(ref _sizeText, value); }
    public string SpeedText { get => _speedText; set => SetProperty(ref _speedText, value); }
    public string EtaText { get => _etaText; set => SetProperty(ref _etaText, value); }
    public string Detail { get => _detail; set => SetProperty(ref _detail, value); }
    public string Error { get => _error; set => SetProperty(ref _error, value); }
    public double Progress { get => _progress; set => SetProperty(ref _progress, value); }
    public DownloadStatus Status { get => _status; set => SetProperty(ref _status, value); }
    public string SavePath { get => _savePath; set => SetProperty(ref _savePath, value); }
    public string Category { get => _category; set => SetProperty(ref _category, value); }
    public long Speed { get => _speed; set => SetProperty(ref _speed, value); }

    public bool IsActive => Status is DownloadStatus.Downloading or DownloadStatus.Connecting;
    public bool CanPause => Status is DownloadStatus.Downloading or DownloadStatus.Connecting or DownloadStatus.Queued;
    public bool CanResume => Status is DownloadStatus.Paused or DownloadStatus.Failed or DownloadStatus.Canceled;
    public bool IsCompleted => Status == DownloadStatus.Completed;

    public void Apply(DownloadJob job, DownloadProgress? progress)
    {
        Id = job.Id;
        FileName = job.FileName;
        Url = job.Url;
        Status = job.Status;
        StatusText = ByteFormatter.StatusKo(job.Status);
        SavePath = job.SavePath;
        Category = job.Category;
        Error = job.ErrorMessage ?? "";
        var downloaded = progress?.DownloadedBytes ?? job.DownloadedBytes;
        var total = progress?.TotalBytes > 0 ? progress.TotalBytes : job.TotalBytes;
        Progress = total > 0 ? Math.Clamp(100.0 * downloaded / total, 0, 100) : (job.Status == DownloadStatus.Completed ? 100 : 0);
        SizeText = total > 0
            ? $"{ByteFormatter.Bytes(downloaded)} / {ByteFormatter.Bytes(total)}"
            : ByteFormatter.Bytes(downloaded);
        Speed = progress?.BytesPerSecond ?? 0;
        SpeedText = IsActive && Speed > 0 ? ByteFormatter.Speed(Speed) : "—";
        EtaText = IsActive ? ByteFormatter.Eta(progress?.Eta) : "—";
        var conn = progress?.ActiveConnections > 0 ? progress.ActiveConnections : job.Connections;
        Detail = job.Status switch
        {
            DownloadStatus.Downloading => $"{conn} 연결 · {SpeedText} · 남은 시간 {EtaText}",
            DownloadStatus.Completed => job.CompletedAt is { } t ? t.ToString("yyyy-MM-dd HH:mm") : "완료",
            DownloadStatus.Failed => job.ErrorMessage ?? "실패",
            DownloadStatus.Paused => "일시정지됨",
            DownloadStatus.Scheduled => job.ScheduledAt is { } s ? $"예약 {s:MM-dd HH:mm}" : "예약",
            _ => StatusText
        };
        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(CanPause));
        OnPropertyChanged(nameof(CanResume));
        OnPropertyChanged(nameof(IsCompleted));
    }
}
