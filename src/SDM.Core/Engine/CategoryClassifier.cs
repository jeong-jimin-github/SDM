namespace SDM.Core.Engine;

public static class CategoryClassifier
{
    public const string Compressed = "compressed";
    public const string Documents = "documents";
    public const string Music = "music";
    public const string Video = "video";
    public const string Programs = "programs";
    public const string Images = "images";
    public const string General = "general";

    public static readonly IReadOnlyList<(string Id, string TitleKo)> All =
    [
        (General, "전체"),
        (Compressed, "압축"),
        (Documents, "문서"),
        (Music, "음악"),
        (Video, "비디오"),
        (Programs, "프로그램"),
        (Images, "이미지")
    ];

    private static readonly HashSet<string> CompressedExt = [".zip", ".rar", ".7z", ".tar", ".gz", ".bz2", ".xz", ".iso", ".cab", ".lz", ".zst"];
    private static readonly HashSet<string> DocumentExt = [".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".hwp", ".odt", ".rtf", ".epub", ".csv"];
    private static readonly HashSet<string> MusicExt = [".mp3", ".flac", ".wav", ".aac", ".ogg", ".m4a", ".wma", ".opus"];
    private static readonly HashSet<string> VideoExt = [".mp4", ".mkv", ".avi", ".mov", ".wmv", ".webm", ".m4v", ".flv", ".ts", ".m3u8"];
    private static readonly HashSet<string> ProgramExt = [".exe", ".msi", ".apk", ".dmg", ".deb", ".rpm", ".msix", ".appx", ".jar", ".bat", ".ps1"];
    private static readonly HashSet<string> ImageExt = [".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp", ".svg", ".psd", ".ai", ".tif", ".tiff", ".heic"];

    public static string FromFileName(string? fileName, string? mime = null)
    {
        var ext = Path.GetExtension(fileName ?? "").ToLowerInvariant();
        if (CompressedExt.Contains(ext)) return Compressed;
        if (DocumentExt.Contains(ext)) return Documents;
        if (MusicExt.Contains(ext)) return Music;
        if (VideoExt.Contains(ext)) return Video;
        if (ProgramExt.Contains(ext)) return Programs;
        if (ImageExt.Contains(ext)) return Images;

        mime = mime?.ToLowerInvariant() ?? "";
        if (mime.StartsWith("video/")) return Video;
        if (mime.StartsWith("audio/")) return Music;
        if (mime.StartsWith("image/")) return Images;
        if (mime.Contains("zip") || mime.Contains("compressed") || mime.Contains("tar")) return Compressed;
        if (mime.Contains("pdf") || mime.Contains("msword") || mime.Contains("officedocument")) return Documents;
        return General;
    }

    public static string SubfolderName(string category) => category switch
    {
        Compressed => "Compressed",
        Documents => "Documents",
        Music => "Music",
        Video => "Video",
        Programs => "Programs",
        Images => "Images",
        _ => "General"
    };
}
