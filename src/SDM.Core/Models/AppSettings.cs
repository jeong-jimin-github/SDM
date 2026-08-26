namespace SDM.Core.Models;

public sealed class AppSettings
{
    public string DefaultDownloadFolder { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "SDM");

    public Dictionary<string, string> CategoryFolders { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public int DefaultConnections { get; set; } = 8;
    public int MaxConcurrentDownloads { get; set; } = 3;
    public long SpeedLimitBytesPerSecond { get; set; }
    public bool WatchClipboard { get; set; } = true;
    public bool AutoStartClipboardUrls { get; set; }
    public bool InterceptBrowserDownloads { get; set; } = true;
    public bool LaunchAtStartup { get; set; }
    public bool CloseToTray { get; set; } = true;
    public bool MinimizeToTray { get; set; } = true;
    public bool AutoResumeOnStart { get; set; } = true;
    public bool ConfirmBeforeAdd { get; set; } = true;
    public bool ShowNotifications { get; set; } = true;
    public int HttpPort { get; set; } = 47832;
    public string IpcToken { get; set; } = Guid.NewGuid().ToString("N");
    public List<string> InterceptExtensions { get; set; } = DefaultInterceptExtensions.ToList();
    public long InterceptMinBytes { get; set; }
    public string? UserAgentOverride { get; set; }
    public bool UseCategorySubfolders { get; set; } = true;

    public static readonly string[] DefaultInterceptExtensions =
    [
        ".zip", ".rar", ".7z", ".tar", ".gz", ".iso", ".bin",
        ".exe", ".msi", ".msix", ".apk", ".dmg",
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".hwp",
        ".mp3", ".flac", ".wav", ".aac", ".ogg", ".m4a",
        ".mp4", ".mkv", ".avi", ".mov", ".webm", ".wmv", ".m4v",
        ".jpg", ".jpeg", ".png", ".webp", ".gif", ".psd", ".ai",
        ".epub", ".torrent"
    ];
}
