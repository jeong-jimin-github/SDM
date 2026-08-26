using System.Net;
using System.Text.RegularExpressions;

namespace SDM.Core.Engine;

public static class FileNameHelper
{
    private static readonly Regex ContentDisposition =
        new(@"filename\*?=(?:UTF-8''(?<utf8>[^;]+)|""(?<quoted>[^""]+)""|(?<bare>[^;]+))",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly HashSet<string> PlaceholderExt =
    [
        ".crdownload", ".tmp", ".part", ".sdmpart", ".download", ".partial",
        ".php", ".aspx", ".asp", ".jsp", ".cgi", ".do", ".action", ".html", ".htm"
    ];

    public static string Sanitize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "download";
        name = name.Trim().Trim('"').Trim('\'');
        try { name = Uri.UnescapeDataString(name.Replace("+", " ")); }
        catch { /* keep raw */ }
        if (name.Contains('\\') || name.Contains('/'))
            name = Path.GetFileName(name.Replace('/', Path.DirectorySeparatorChar));
        name = name.Replace("\0", "");
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        name = name.Trim().TrimEnd('.');
        if (name.EndsWith(".crdownload", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith(".partial", StringComparison.OrdinalIgnoreCase))
            name = Path.GetFileNameWithoutExtension(name);
        if (name.Length > 180) name = name[..180];
        return string.IsNullOrWhiteSpace(name) ? "download" : name;
    }

    public static bool IsPlaceholder(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return true;
        var n = name.Trim();
        var ext = Path.GetExtension(n).ToLowerInvariant();
        var stem = Path.GetFileNameWithoutExtension(n);
        if (n.Equals("download", StringComparison.OrdinalIgnoreCase)) return true;
        if (n.Equals("download.bin", StringComparison.OrdinalIgnoreCase)) return true;
        if (n.Equals("file", StringComparison.OrdinalIgnoreCase)) return true;
        if (n.Equals("unknown", StringComparison.OrdinalIgnoreCase)) return true;
        if (stem.StartsWith("Unconfirmed", StringComparison.OrdinalIgnoreCase)) return true;
        if (PlaceholderExt.Contains(ext)) return true;
        if (ext.Length == 0 && stem.Length is >= 16 and <= 64 && stem.All(IsHashChar))
            return true;
        return false;
    }

    private static bool IsHashChar(char c) =>
        char.IsAsciiHexDigit(c) || c is '-' or '_';

    public static string FromUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            var last = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
            if (!string.IsNullOrWhiteSpace(last))
                return Sanitize(last);
        }
        catch
        {
            // ignored
        }
        return "download";
    }

    public static string? FromContentDisposition(string? header)
    {
        if (string.IsNullOrWhiteSpace(header)) return null;
        var m = ContentDisposition.Match(header);
        if (!m.Success) return null;
        if (m.Groups["utf8"].Success)
            return Sanitize(m.Groups["utf8"].Value);
        if (m.Groups["quoted"].Success)
            return Sanitize(m.Groups["quoted"].Value);
        if (m.Groups["bare"].Success)
            return Sanitize(m.Groups["bare"].Value.Trim());
        return null;
    }

    public static string Resolve(string? suggested, string url, string? contentDisposition = null, string? mime = null)
    {
        var fromCd = FromContentDisposition(contentDisposition);
        var fromSuggested = string.IsNullOrWhiteSpace(suggested) ? null : Sanitize(suggested);
        var fromUrl = FromUrl(url);

        string pick;
        if (!string.IsNullOrWhiteSpace(fromCd) && !IsPlaceholder(fromCd))
            pick = fromCd;
        else if (!string.IsNullOrWhiteSpace(fromSuggested) && !IsPlaceholder(fromSuggested))
            pick = fromSuggested;
        else if (!string.IsNullOrWhiteSpace(fromUrl) && !IsPlaceholder(fromUrl))
            pick = fromUrl;
        else
            pick = fromCd ?? fromSuggested ?? fromUrl ?? "download";

        return ApplyMimeExtension(pick, mime);
    }

    public static string ApplyMimeExtension(string fileName, string? mime)
    {
        var ext = Path.GetExtension(fileName);
        var mimeExt = MimeTypes.Extension(mime);
        if (string.IsNullOrEmpty(ext) || PlaceholderExt.Contains(ext.ToLowerInvariant()))
        {
            if (mimeExt is not null)
            {
                var stem = Path.GetFileNameWithoutExtension(fileName);
                if (string.IsNullOrWhiteSpace(stem) || IsPlaceholder(stem))
                    stem = "download";
                return stem + mimeExt;
            }
        }
        return fileName;
    }

    public static string CorrectWithSignature(string fileName, string? detectedExt)
    {
        if (string.IsNullOrWhiteSpace(detectedExt)) return fileName;
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (ext == detectedExt.ToLowerInvariant()) return fileName;
        if (detectedExt.Equals(".zip", StringComparison.OrdinalIgnoreCase) &&
            MimeTypes.ZipContainerExtension(ext))
            return fileName;
        if (detectedExt.Equals(".exe", StringComparison.OrdinalIgnoreCase) &&
            ext is ".dll" or ".msi" or ".scr" or ".com")
            return fileName;

        if (string.IsNullOrEmpty(ext) || IsPlaceholder(fileName) ||
            PlaceholderExt.Contains(ext) || ext is ".bin" or ".dat")
        {
            var stem = Path.GetFileNameWithoutExtension(fileName);
            if (string.IsNullOrWhiteSpace(stem) || stem.StartsWith("Unconfirmed", StringComparison.OrdinalIgnoreCase))
                stem = "download";
            return stem + detectedExt;
        }

        return fileName;
    }

    public static string UniquePath(string directory, string fileName)
    {
        Directory.CreateDirectory(directory);
        var dest = Path.Combine(directory, fileName);
        if (!File.Exists(dest) && !File.Exists(dest + ".sdmpart")) return dest;

        var name = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);
        for (var i = 1; i < 10_000; i++)
        {
            var candidate = Path.Combine(directory, $"{name} ({i}){ext}");
            if (!File.Exists(candidate) && !File.Exists(candidate + ".sdmpart"))
                return candidate;
        }
        return Path.Combine(directory, $"{name}-{Guid.NewGuid():N}{ext}");
    }

    public static bool LooksLikeUrl(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        text = text.Trim();
        return Uri.TryCreate(text, UriKind.Absolute, out var uri)
               && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    public static string? ExtractUrl(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        text = text.Trim().Trim('"');
        if (LooksLikeUrl(text)) return text;
        var match = Regex.Match(text, @"https?://[^\s<>""']+", RegexOptions.IgnoreCase);
        return match.Success ? match.Value : null;
    }

    public static string Decode(string value) => WebUtility.UrlDecode(value) ?? value;
}
