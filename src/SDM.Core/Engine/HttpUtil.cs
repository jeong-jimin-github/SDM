using System.Net;
using System.Net.Http.Headers;
using SDM.Core.Models;

namespace SDM.Core.Engine;

internal static class HttpUtil
{
    public const string DefaultUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36 SDM/1.0";

    public static HttpClient CreateClient()
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.None,
            AllowAutoRedirect = true,
            ConnectTimeout = TimeSpan.FromSeconds(25),
            PooledConnectionLifetime = TimeSpan.FromMinutes(3),
            MaxConnectionsPerServer = 32,
            EnableMultipleHttp2Connections = true
        };
        return new HttpClient(handler, disposeHandler: true)
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
    }

    public static HttpRequestMessage CreateRequest(HttpMethod method, DownloadJob job, string url, long? rangeStart = null, long? rangeEnd = null)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.TryAddWithoutValidation("User-Agent",
            string.IsNullOrWhiteSpace(job.UserAgent) ? DefaultUserAgent : job.UserAgent);
        req.Headers.TryAddWithoutValidation("Accept", "*/*");
        req.Headers.ConnectionClose = false;

        if (!string.IsNullOrWhiteSpace(job.Referrer))
            req.Headers.TryAddWithoutValidation("Referer", job.Referrer);
        if (!string.IsNullOrWhiteSpace(job.Cookies))
            req.Headers.TryAddWithoutValidation("Cookie", job.Cookies);

        foreach (var (k, v) in job.Headers)
        {
            if (k.Equals("Host", StringComparison.OrdinalIgnoreCase) ||
                k.Equals("Content-Length", StringComparison.OrdinalIgnoreCase) ||
                k.Equals("Range", StringComparison.OrdinalIgnoreCase))
                continue;
            req.Headers.TryAddWithoutValidation(k, v);
        }

        if (rangeStart is not null)
        {
            req.Headers.Range = rangeEnd is not null
                ? new RangeHeaderValue(rangeStart, rangeEnd)
                : new RangeHeaderValue(rangeStart, null);
        }

        return req;
    }

    public static long? ParseContentRangeTotal(string? contentRange)
    {
        if (string.IsNullOrWhiteSpace(contentRange)) return null;
        var slash = contentRange.LastIndexOf('/');
        if (slash < 0 || slash == contentRange.Length - 1) return null;
        var total = contentRange[(slash + 1)..].Trim();
        if (total == "*") return null;
        return long.TryParse(total, out var n) ? n : null;
    }
}
