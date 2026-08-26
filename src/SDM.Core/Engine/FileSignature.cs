namespace SDM.Core.Engine;

public static class FileSignature
{
    public static string? DetectExtension(string path)
    {
        try
        {
            using var fs = File.OpenRead(path);
            Span<byte> buf = stackalloc byte[16];
            var n = fs.Read(buf);
            if (n < 4) return null;
            return Detect(buf[..n]);
        }
        catch
        {
            return null;
        }
    }

    public static string? Detect(ReadOnlySpan<byte> b)
    {
        if (b.Length >= 2 && b[0] == (byte)'M' && b[1] == (byte)'Z') return ".exe";
        if (b.Length >= 5 && b[0] == (byte)'%' && b[1] == (byte)'P' && b[2] == (byte)'D' && b[3] == (byte)'F')
            return ".pdf";
        if (b.Length >= 4 && b[0] == 0x50 && b[1] == 0x4B && (b[2] == 0x03 || b[2] == 0x05 || b[2] == 0x07) &&
            (b[3] == 0x04 || b[3] == 0x06 || b[3] == 0x08))
            return ".zip";
        if (b.Length >= 7 && b[0] == (byte)'R' && b[1] == (byte)'a' && b[2] == (byte)'r' && b[3] == (byte)'!')
            return ".rar";
        if (b.Length >= 6 && b[0] == (byte)'7' && b[1] == (byte)'z' && b[2] == 0xBC && b[3] == 0xAF)
            return ".7z";
        if (b.Length >= 2 && b[0] == 0x1F && b[1] == 0x8B) return ".gz";
        if (b.Length >= 4 && b[0] == 0x89 && b[1] == (byte)'P' && b[2] == (byte)'N' && b[3] == (byte)'G')
            return ".png";
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return ".jpg";
        if (b.Length >= 3 && b[0] == (byte)'G' && b[1] == (byte)'I' && b[2] == (byte)'F') return ".gif";
        if (b.Length >= 12 && b[0] == (byte)'R' && b[1] == (byte)'I' && b[2] == (byte)'F' && b[3] == (byte)'F')
        {
            if (b.Length >= 12 && b[8] == (byte)'W' && b[9] == (byte)'E' && b[10] == (byte)'B' && b[11] == (byte)'P')
                return ".webp";
            if (b.Length >= 12 && b[8] == (byte)'W' && b[9] == (byte)'A' && b[10] == (byte)'V' && b[11] == (byte)'E')
                return ".wav";
            if (b.Length >= 12 && b[8] == (byte)'A' && b[9] == (byte)'V' && b[10] == (byte)'I')
                return ".avi";
        }
        if (b.Length >= 12 && b[4] == (byte)'f' && b[5] == (byte)'t' && b[6] == (byte)'y' && b[7] == (byte)'p')
            return ".mp4";
        if (b.Length >= 4 && b[0] == 0x1A && b[1] == 0x45 && b[2] == 0xDF && b[3] == 0xA3)
            return ".mkv";
        if (b.Length >= 3 && b[0] == (byte)'I' && b[1] == (byte)'D' && b[2] == (byte)'3') return ".mp3";
        if (b.Length >= 2 && b[0] == 0xFF && (b[1] == 0xFB || b[1] == 0xF3 || b[1] == 0xF2)) return ".mp3";
        if (b.Length >= 4 && b[0] == (byte)'O' && b[1] == (byte)'g' && b[2] == (byte)'g' && b[3] == (byte)'S')
            return ".ogg";
        if (b.Length >= 4 && b[0] == 0x00 && b[1] == 0x00 && b[2] == 0x01 && b[3] == 0xBA) return ".mpg";
        if (b.Length >= 4 && b[0] == (byte)'D' && b[1] == (byte)'I' && b[2] == (byte)'C' && b[3] == (byte)'M')
            return ".dcm";
        if (b.Length >= 8 && b[0] == 0xD0 && b[1] == 0xCF && b[2] == 0x11 && b[3] == 0xE0) return ".doc";
        return null;
    }
}

public static class MimeTypes
{
    public static string? Extension(string? mime)
    {
        if (string.IsNullOrWhiteSpace(mime)) return null;
        var t = mime.Split(';')[0].Trim().ToLowerInvariant();
        return t switch
        {
            "application/pdf" => ".pdf",
            "application/zip" or "application/x-zip-compressed" or "application/x-zip" => ".zip",
            "application/x-7z-compressed" => ".7z",
            "application/x-rar-compressed" or "application/vnd.rar" => ".rar",
            "application/x-msdownload" or "application/vnd.microsoft.portable-executable"
                or "application/x-msdos-program" or "application/exe" => ".exe",
            "application/x-msi" or "application/x-ole-storage" => ".msi",
            "application/vnd.android.package-archive" => ".apk",
            "application/x-iso9660-image" => ".iso",
            "application/gzip" or "application/x-gzip" => ".gz",
            "application/x-bzip2" => ".bz2",
            "application/octet-stream" => null,
            "video/mp4" => ".mp4",
            "video/webm" => ".webm",
            "video/x-matroska" => ".mkv",
            "video/quicktime" => ".mov",
            "video/x-msvideo" => ".avi",
            "audio/mpeg" => ".mp3",
            "audio/mp4" or "audio/aac" => ".m4a",
            "audio/wav" or "audio/x-wav" => ".wav",
            "audio/flac" => ".flac",
            "audio/ogg" => ".ogg",
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/gif" => ".gif",
            "image/webp" => ".webp",
            "image/bmp" => ".bmp",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => ".docx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => ".xlsx",
            "application/vnd.openxmlformats-officedocument.presentationml.presentation" => ".pptx",
            "application/msword" => ".doc",
            "application/vnd.ms-excel" => ".xls",
            "application/vnd.ms-powerpoint" => ".ppt",
            "application/epub+zip" => ".epub",
            "application/x-bittorrent" => ".torrent",
            _ when t.StartsWith("video/") => ".mp4",
            _ when t.StartsWith("audio/") => ".mp3",
            _ when t.StartsWith("image/") => ".img",
            _ => null
        };
    }

    public static bool ZipContainerExtension(string ext) =>
        ext is ".zip" or ".docx" or ".xlsx" or ".pptx" or ".apk" or ".jar" or ".epub" or ".msix" or ".appx" or ".odt" or ".ods";
}
