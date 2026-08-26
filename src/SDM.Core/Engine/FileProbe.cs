using System.Net;
using SDM.Core.Models;

namespace SDM.Core.Engine;

public sealed class ProbeResult
{
    public string FinalUrl { get; init; } = "";
    public string? FileName { get; init; }
    public string? Mime { get; init; }
    public long TotalBytes { get; init; }
    public bool SupportsRanges { get; init; }
}

public static class FileProbe
{
    public static async Task<ProbeResult> ProbeAsync(DownloadJob job, CancellationToken ct)
    {
        using var client = HttpUtil.CreateClient();
        return await ProbeAsync(client, job, job.FinalUrl ?? job.Url, ct).ConfigureAwait(false);
    }

    internal static async Task<ProbeResult> ProbeAsync(
        HttpClient client, DownloadJob job, string url, CancellationToken ct)
    {
        async Task<HttpResponseMessage> SendAsync(HttpMethod method, long? start, long? end)
        {
            using var req = HttpUtil.CreateRequest(method, job, url, start, end);
            return await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        }

        HttpResponseMessage? resp = null;
        try
        {
            resp = await SendAsync(HttpMethod.Get, 0, 0).ConfigureAwait(false);
            if ((int)resp.StatusCode is 405 or 501 or 400)
            {
                resp.Dispose();
                resp = await SendAsync(HttpMethod.Head, null, null).ConfigureAwait(false);
            }

            if ((int)resp.StatusCode >= 400 && resp.StatusCode != HttpStatusCode.RequestedRangeNotSatisfiable)
            {
                resp.Dispose();
                resp = await SendAsync(HttpMethod.Get, null, null).ConfigureAwait(false);
            }

            resp.EnsureSuccessStatusCode();
            var finalUrl = resp.RequestMessage?.RequestUri?.ToString() ?? url;

            var cd = resp.Content.Headers.ContentDisposition;
            string? disposition = cd?.ToString();
            if (string.IsNullOrWhiteSpace(disposition) &&
                resp.Content.Headers.TryGetValues("Content-Disposition", out var values))
                disposition = values.FirstOrDefault();

            var mime = resp.Content.Headers.ContentType?.MediaType ?? job.Mime;
            var total = HttpUtil.ParseContentRangeTotal(resp.Content.Headers.ContentRange?.ToString())
                        ?? resp.Content.Headers.ContentLength;
            var ranges = resp.StatusCode == HttpStatusCode.PartialContent
                         || resp.Headers.AcceptRanges.Contains("bytes");
            if (resp.StatusCode == HttpStatusCode.OK && (resp.Content.Headers.ContentLength ?? 0) <= 1)
                ranges = false;

            var suggested = cd?.FileNameStar ?? cd?.FileName ?? job.FileName;
            var name = FileNameHelper.Resolve(suggested, finalUrl, disposition, mime);

            return new ProbeResult
            {
                FinalUrl = finalUrl,
                FileName = name,
                Mime = mime,
                TotalBytes = total ?? 0,
                SupportsRanges = ranges && total is > 1
            };
        }
        finally
        {
            resp?.Dispose();
        }
    }
}
