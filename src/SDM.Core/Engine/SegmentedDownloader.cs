using System.Diagnostics;
using System.Net;
using SDM.Core.Models;

namespace SDM.Core.Engine;

public sealed class SegmentedDownloader
{
    private readonly SpeedLimiter _limiter;
    private const int BufferSize = 64 * 1024;
    private const int MaxRetries = 5;

    public SegmentedDownloader(SpeedLimiter limiter) => _limiter = limiter;

    public async Task RunAsync(DownloadJob job, IProgress<DownloadProgress> progress, CancellationToken ct)
    {
        using var client = HttpUtil.CreateClient();
        var url = job.FinalUrl ?? job.Url;
        job.Status = DownloadStatus.Connecting;
        Report(progress, job, 0, 0);

        var probe = await FileProbe.ProbeAsync(client, job, url, ct).ConfigureAwait(false);
        url = probe.FinalUrl;
        job.FinalUrl = url;
        job.SupportsRanges = probe.SupportsRanges && probe.TotalBytes > 0;
        if (probe.TotalBytes > 0) job.TotalBytes = probe.TotalBytes;
        if (!string.IsNullOrWhiteSpace(probe.Mime)) job.Mime = probe.Mime;
        if (!string.IsNullOrWhiteSpace(probe.FileName))
        {
            job.FileName = probe.FileName;
            job.Category = CategoryClassifier.FromFileName(job.FileName, job.Mime);
        }

        Directory.CreateDirectory(job.SaveDirectory);

        job.Status = DownloadStatus.Downloading;
        job.StartedAt ??= DateTime.Now;

        var speed = new SpeedTracker();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var runToken = linked.Token;

        try
        {
            if (!job.SupportsRanges || job.Connections <= 1 || job.TotalBytes <= 0)
            {
                job.Connections = 1;
                job.Segments.Clear();
                await DownloadSingleAsync(client, job, url, speed, progress, runToken).ConfigureAwait(false);
            }
            else
            {
                try
                {
                    EnsureSegments(job);
                    Preallocate(job);
                    var workers = job.Segments
                        .Where(s => !s.IsComplete)
                        .Select(s => DownloadSegmentAsync(client, job, url, s, speed, progress, runToken));
                    await Task.WhenAll(workers).ConfigureAwait(false);
                }
                catch (InvalidOperationException)
                {
                    job.SupportsRanges = false;
                    job.Connections = 1;
                    job.Segments.Clear();
                    await DownloadSingleAsync(client, job, url, speed, progress, runToken).ConfigureAwait(false);
                }
            }

            job.DownloadedBytes = job.TotalBytes > 0
                ? job.TotalBytes
                : new FileInfo(job.TempPath).Length;
            FinalizeFile(job);
            job.Status = DownloadStatus.Completed;
            job.CompletedAt = DateTime.Now;
            job.ErrorMessage = null;
            Report(progress, job, speed.BytesPerSecond, 0);
        }
        catch (OperationCanceledException)
        {
            RecalcDownloaded(job);
            if (job.Status != DownloadStatus.Paused)
                job.Status = DownloadStatus.Canceled;
            Report(progress, job, 0, job.Connections);
            throw;
        }
        catch (Exception ex)
        {
            RecalcDownloaded(job);
            job.Status = DownloadStatus.Failed;
            job.ErrorMessage = ex.Message;
            Report(progress, job, 0, 0);
            throw;
        }
    }

    private static void EnsureSegments(DownloadJob job)
    {
        var n = Math.Clamp(job.Connections, 1, 32);
        job.Connections = n;
        var size = job.TotalBytes;
        if (job.Segments.Count == n && job.Segments.All(s => s.End < size || s.Index == n - 1))
        {
            job.Segments[^1].End = size - 1;
            return;
        }

        job.Segments.Clear();
        var chunk = size / n;
        long pos = 0;
        for (var i = 0; i < n; i++)
        {
            var end = i == n - 1 ? size - 1 : pos + chunk - 1;
            job.Segments.Add(new SegmentState { Index = i, Start = pos, End = end, Written = 0 });
            pos = end + 1;
        }
    }

    private static void Preallocate(DownloadJob job)
    {
        using var fs = new FileStream(job.TempPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite);
        if (job.TotalBytes > 0 && fs.Length != job.TotalBytes)
            fs.SetLength(job.TotalBytes);
    }

    private async Task DownloadSingleAsync(
        HttpClient client, DownloadJob job, string url, SpeedTracker speed,
        IProgress<DownloadProgress> progress, CancellationToken ct)
    {
        var existing = File.Exists(job.TempPath) ? new FileInfo(job.TempPath).Length : 0;
        var resume = existing > 0 && job.SupportsRanges;

        for (var attempt = 0; attempt <= MaxRetries; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                using var req = HttpUtil.CreateRequest(HttpMethod.Get, job, url, resume ? existing : null, null);
                using var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct)
                    .ConfigureAwait(false);
                resp.EnsureSuccessStatusCode();

                if (!resume)
                {
                    existing = 0;
                    job.DownloadedBytes = 0;
                }

                if (resp.Content.Headers.ContentLength is long remaining && job.TotalBytes <= 0)
                    job.TotalBytes = existing + remaining;

                await using var input = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                await using var output = new FileStream(
                    job.TempPath, resume ? FileMode.Append : FileMode.Create,
                    FileAccess.Write, FileShare.Read, BufferSize, FileOptions.Asynchronous);

                await CopyAsync(input, output, job, () =>
                {
                    job.DownloadedBytes = output.Length;
                    return 1;
                }, speed, progress, ct).ConfigureAwait(false);

                job.DownloadedBytes = output.Length;
                if (job.TotalBytes <= 0) job.TotalBytes = output.Length;
                return;
            }
            catch (OperationCanceledException) { throw; }
            catch when (attempt < MaxRetries)
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct).ConfigureAwait(false);
                existing = File.Exists(job.TempPath) ? new FileInfo(job.TempPath).Length : 0;
                resume = existing > 0;
            }
        }
    }

    private async Task DownloadSegmentAsync(
        HttpClient client, DownloadJob job, string url, SegmentState segment,
        SpeedTracker speed, IProgress<DownloadProgress> progress, CancellationToken ct)
    {
        for (var attempt = 0; attempt <= MaxRetries; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            if (segment.IsComplete) return;
            try
            {
                var start = segment.AbsolutePosition;
                var end = segment.End;
                using var req = HttpUtil.CreateRequest(HttpMethod.Get, job, url, start, end);
                using var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct)
                    .ConfigureAwait(false);
                if (resp.StatusCode is HttpStatusCode.OK && segment.Start > 0 && segment.Written == 0)
                    throw new InvalidOperationException("서버가 Range 요청을 무시했습니다. 단일 연결로 다시 시도하세요.");
                resp.EnsureSuccessStatusCode();

                await using var input = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                await using var output = new FileStream(
                    job.TempPath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite,
                    BufferSize, FileOptions.Asynchronous);
                output.Seek(start, SeekOrigin.Begin);

                await CopyAsync(input, output, job, () =>
                {
                    RecalcDownloaded(job);
                    return job.Segments.Count(s => !s.IsComplete);
                }, speed, progress, ct, segment).ConfigureAwait(false);

                if (segment.IsComplete) return;
            }
            catch (OperationCanceledException) { throw; }
            catch when (attempt < MaxRetries)
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct).ConfigureAwait(false);
            }
        }

        throw new IOException($"세그먼트 {segment.Index} 다운로드에 실패했습니다.");
    }

    private async Task CopyAsync(
        Stream input, Stream output, DownloadJob job,
        Func<int> activeConnections, SpeedTracker speed,
        IProgress<DownloadProgress> progress, CancellationToken ct,
        SegmentState? segment = null)
    {
        var buffer = new byte[BufferSize];
        var lastReport = Stopwatch.GetTimestamp();
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var max = buffer.Length;
            if (segment is not null)
                max = (int)Math.Min(max, segment.Remaining);
            if (max <= 0) break;

            var read = await input.ReadAsync(buffer.AsMemory(0, max), ct).ConfigureAwait(false);
            if (read <= 0) break;

            await _limiter.ConsumeAsync(read, ct).ConfigureAwait(false);
            await output.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
            if (segment is not null) segment.Written += read;
            speed.Add(read);

            var now = Stopwatch.GetTimestamp();
            if (now - lastReport > Stopwatch.Frequency / 5)
            {
                lastReport = now;
                RecalcDownloaded(job);
                var bps = speed.BytesPerSecond;
                TimeSpan? eta = bps > 0 && job.TotalBytes > job.DownloadedBytes
                    ? TimeSpan.FromSeconds((job.TotalBytes - job.DownloadedBytes) / (double)bps)
                    : null;
                progress.Report(new DownloadProgress
                {
                    JobId = job.Id,
                    Status = DownloadStatus.Downloading,
                    DownloadedBytes = job.DownloadedBytes,
                    TotalBytes = job.TotalBytes,
                    BytesPerSecond = bps,
                    Eta = eta,
                    ActiveConnections = activeConnections(),
                    FileName = job.FileName
                });
            }
        }

        await output.FlushAsync(ct).ConfigureAwait(false);
    }

    private static void RecalcDownloaded(DownloadJob job)
    {
        if (job.Segments.Count > 0)
            job.DownloadedBytes = job.Segments.Sum(s => s.Written);
        else if (File.Exists(job.TempPath))
            job.DownloadedBytes = new FileInfo(job.TempPath).Length;
    }

    private static void FinalizeFile(DownloadJob job)
    {
        if (!File.Exists(job.TempPath))
            throw new FileNotFoundException("임시 파일이 없습니다.", job.TempPath);

        var detected = FileSignature.DetectExtension(job.TempPath);
        job.FileName = FileNameHelper.CorrectWithSignature(job.FileName, detected);

        if (File.Exists(job.SavePath))
        {
            var unique = FileNameHelper.UniquePath(job.SaveDirectory, job.FileName);
            job.FileName = Path.GetFileName(unique);
        }

        File.Move(job.TempPath, job.SavePath, overwrite: false);
    }

    private static void Report(IProgress<DownloadProgress> progress, DownloadJob job, long bps, int active)
    {
        progress.Report(new DownloadProgress
        {
            JobId = job.Id,
            Status = job.Status,
            DownloadedBytes = job.DownloadedBytes,
            TotalBytes = job.TotalBytes,
            BytesPerSecond = bps,
            ActiveConnections = active,
            ErrorMessage = job.ErrorMessage,
            FileName = job.FileName
        });
    }

    private sealed class SpeedTracker
    {
        private readonly Queue<(long Ticks, long Bytes)> _window = new();
        private long _total;
        private readonly object _gate = new();

        public void Add(int bytes)
        {
            lock (_gate)
            {
                var now = Stopwatch.GetTimestamp();
                _window.Enqueue((now, bytes));
                _total += bytes;
                var cutoff = now - Stopwatch.Frequency * 3;
                while (_window.Count > 0 && _window.Peek().Ticks < cutoff)
                {
                    _total -= _window.Dequeue().Bytes;
                }
            }
        }

        public long BytesPerSecond
        {
            get
            {
                lock (_gate)
                {
                    if (_window.Count < 2) return 0;
                    var dt = (_window.Last().Ticks - _window.Peek().Ticks) / (double)Stopwatch.Frequency;
                    return dt <= 0 ? 0 : (long)(_total / dt);
                }
            }
        }
    }
}
