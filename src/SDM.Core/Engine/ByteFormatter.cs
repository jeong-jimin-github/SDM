namespace SDM.Core.Engine;

public static class ByteFormatter
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB"];

    public static string Bytes(long bytes)
    {
        if (bytes < 0) bytes = 0;
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return unit == 0 ? $"{bytes} B" : $"{value:0.##} {Units[unit]}";
    }

    public static string Speed(long bytesPerSecond) => $"{Bytes(bytesPerSecond)}/s";

    public static string Eta(TimeSpan? eta)
    {
        if (eta is null) return "—";
        var t = eta.Value;
        if (t.TotalHours >= 24) return $"{(int)t.TotalDays}일 {t.Hours}시간";
        if (t.TotalHours >= 1) return $"{(int)t.TotalHours}시간 {t.Minutes:00}분";
        if (t.TotalMinutes >= 1) return $"{t.Minutes}분 {t.Seconds:00}초";
        return $"{Math.Max(0, (int)t.TotalSeconds)}초";
    }

    public static string StatusKo(Models.DownloadStatus status) => status switch
    {
        Models.DownloadStatus.Queued => "대기",
        Models.DownloadStatus.Connecting => "연결 중",
        Models.DownloadStatus.Downloading => "받는 중",
        Models.DownloadStatus.Paused => "일시정지",
        Models.DownloadStatus.Completed => "완료",
        Models.DownloadStatus.Failed => "실패",
        Models.DownloadStatus.Canceled => "취소",
        Models.DownloadStatus.Scheduled => "예약",
        _ => status.ToString()
    };
}
