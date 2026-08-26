using System.Collections.Concurrent;
using System.Diagnostics;
using SDM.Core.Models;
using SDM.Core.Persistence;

namespace SDM.Core.Engine;

public sealed class DownloadManager : IDisposable
{
    private readonly JobStore _store = new();
    private readonly SettingsStore _settingsStore;
    private readonly SpeedLimiter _limiter = new();
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _running = new();
    private readonly ConcurrentDictionary<Guid, long> _speeds = new();
    private readonly object _jobsGate = new();
    private readonly List<DownloadJob> _jobs = [];
    private readonly SemaphoreSlim _persistLock = new(1, 1);
    private CancellationTokenSource _loopCts = new();
    private Task? _loop;

    public DownloadManager(SettingsStore settingsStore) => _settingsStore = settingsStore;

    public AppSettings Settings => _settingsStore.Current;
    public SpeedLimiter Limiter => _limiter;

    public event Action<DownloadJob>? JobAdded;
    public event Action<DownloadJob, DownloadProgress?>? JobUpdated;
    public event Action<Guid>? JobRemoved;
    public event Action<string>? Toast;

    public IReadOnlyList<DownloadJob> Jobs
    {
        get { lock (_jobsGate) return _jobs.Select(JobStore.Clone).ToList(); }
    }

    public void Start()
    {
        _settingsStore.Load();
        _limiter.BytesPerSecond = Settings.SpeedLimitBytesPerSecond;
        _store.Load();
        lock (_jobsGate)
        {
            _jobs.Clear();
            foreach (var job in _store.Snapshot())
            {
                if (job.Status is DownloadStatus.Downloading or DownloadStatus.Connecting)
                    job.Status = Settings.AutoResumeOnStart ? DownloadStatus.Queued : DownloadStatus.Paused;
                _jobs.Add(job);
            }
        }

        _loopCts = new CancellationTokenSource();
        _loop = Task.Run(() => SchedulerLoopAsync(_loopCts.Token));
    }

    public DownloadJob Enqueue(DownloadRequest request)
    {
        var settings = Settings;
        var fileName = FileNameHelper.Resolve(request.FileName, request.Url, mime: request.Mime);
        var category = CategoryClassifier.FromFileName(fileName, request.Mime);
        var directory = ResolveDirectory(settings, category, request.SaveDirectory);
        var unique = FileNameHelper.UniquePath(directory, fileName);

        var job = new DownloadJob
        {
            Url = request.Url.Trim(),
            FileName = Path.GetFileName(unique),
            SaveDirectory = directory,
            Referrer = request.Referrer,
            Cookies = request.Cookies,
            UserAgent = request.UserAgent ?? settings.UserAgentOverride,
            Mime = request.Mime,
            Headers = request.Headers ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            Connections = Math.Clamp(request.Connections ?? settings.DefaultConnections, 1, 32),
            Category = category,
            Status = request.ScheduledAt is { } when && when > DateTime.Now
                ? DownloadStatus.Scheduled
                : request.Paused
                    ? DownloadStatus.Paused
                    : DownloadStatus.Queued,
            ScheduledAt = request.ScheduledAt,
            OpenFolderWhenDone = request.OpenFolderWhenDone
        };

        if (request.FileSize is > 0) job.TotalBytes = request.FileSize.Value;

        lock (_jobsGate) _jobs.Insert(0, job);
        Persist(job);
        JobAdded?.Invoke(JobStore.Clone(job));
        return job;
    }

    public void Pause(Guid id)
    {
        if (_running.TryGetValue(id, out var cts))
        {
            Update(id, j => j.Status = DownloadStatus.Paused);
            cts.Cancel();
        }
        else
        {
            Update(id, j =>
            {
                if (j.Status is DownloadStatus.Queued or DownloadStatus.Scheduled or DownloadStatus.Connecting)
                    j.Status = DownloadStatus.Paused;
            });
        }
    }

    public void Resume(Guid id)
    {
        Update(id, j =>
        {
            if (j.Status is DownloadStatus.Paused or DownloadStatus.Failed or DownloadStatus.Canceled)
            {
                j.Status = DownloadStatus.Queued;
                j.ErrorMessage = null;
            }
        });
    }

    public void Cancel(Guid id)
    {
        if (_running.TryGetValue(id, out var cts))
        {
            Update(id, j => j.Status = DownloadStatus.Canceled);
            cts.Cancel();
        }
        else
        {
            Update(id, j =>
            {
                j.Status = DownloadStatus.Canceled;
                TryDeleteTemp(j);
            });
        }
    }

    public void Remove(Guid id, bool deleteFile)
    {
        Cancel(id);
        DownloadJob? job;
        lock (_jobsGate)
        {
            job = _jobs.FirstOrDefault(j => j.Id == id);
            _jobs.RemoveAll(j => j.Id == id);
        }

        if (job is not null)
        {
            TryDeleteTemp(job);
            if (deleteFile)
            {
                try { if (File.Exists(job.SavePath)) File.Delete(job.SavePath); }
                catch { /* ignore */ }
            }
        }

        _store.Remove(id);
        _speeds.TryRemove(id, out _);
        JobRemoved?.Invoke(id);
    }

    public void PauseAll()
    {
        Guid[] ids;
        lock (_jobsGate)
            ids = _jobs.Where(j => j.Status is DownloadStatus.Downloading or DownloadStatus.Connecting or DownloadStatus.Queued)
                .Select(j => j.Id).ToArray();
        foreach (var id in ids) Pause(id);
    }

    public void ResumeAll()
    {
        Guid[] ids;
        lock (_jobsGate)
            ids = _jobs.Where(j => j.Status is DownloadStatus.Paused or DownloadStatus.Failed)
                .Select(j => j.Id).ToArray();
        foreach (var id in ids) Resume(id);
    }

    public long TotalBytesPerSecond => _speeds.Values.Sum();

    public void ApplySettings()
    {
        _limiter.BytesPerSecond = Settings.SpeedLimitBytesPerSecond;
        _settingsStore.Save();
    }

    public DownloadJob? Find(Guid id)
    {
        lock (_jobsGate) return _jobs.FirstOrDefault(j => j.Id == id) is { } j ? JobStore.Clone(j) : null;
    }

    private async Task SchedulerLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                PromoteScheduled();
                await FillSlotsAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                // keep loop alive
            }

            try { await Task.Delay(250, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }
    }

    private void PromoteScheduled()
    {
        lock (_jobsGate)
        {
            foreach (var job in _jobs.Where(j =>
                         j.Status == DownloadStatus.Scheduled &&
                         j.ScheduledAt is { } at && at <= DateTime.Now))
            {
                job.Status = DownloadStatus.Queued;
                Persist(job);
                JobUpdated?.Invoke(JobStore.Clone(job), null);
            }
        }
    }

    private async Task FillSlotsAsync(CancellationToken ct)
    {
        var max = Math.Max(1, Settings.MaxConcurrentDownloads);
        while (_running.Count < max)
        {
            DownloadJob? next;
            lock (_jobsGate)
                next = _jobs.Where(j => j.Status == DownloadStatus.Queued)
                    .OrderBy(j => j.CreatedAt)
                    .FirstOrDefault();

            if (next is null) break;
            _ = StartJobAsync(next.Id, ct);
        }
    }

    private async Task StartJobAsync(Guid id, CancellationToken loopCt)
    {
        var cts = new CancellationTokenSource();
        if (!_running.TryAdd(id, cts))
        {
            cts.Dispose();
            return;
        }

        DownloadJob? job;
        lock (_jobsGate) job = _jobs.FirstOrDefault(j => j.Id == id);
        if (job is null)
        {
            _running.TryRemove(id, out _);
            cts.Dispose();
            return;
        }

        job.Status = DownloadStatus.Connecting;
        Persist(job);
        JobUpdated?.Invoke(JobStore.Clone(job), null);

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(loopCt, cts.Token);
        var downloader = new SegmentedDownloader(_limiter);
        var lastPersist = Stopwatch.GetTimestamp();
        var progress = new Progress<DownloadProgress>(p =>
        {
            _speeds[id] = p.BytesPerSecond;
            lock (_jobsGate)
            {
                var live = _jobs.FirstOrDefault(j => j.Id == id);
                if (live is null) return;
                live.DownloadedBytes = p.DownloadedBytes;
                live.TotalBytes = p.TotalBytes > 0 ? p.TotalBytes : live.TotalBytes;
                if (!string.IsNullOrWhiteSpace(p.FileName)) live.FileName = p.FileName;
                if (live.Status is not DownloadStatus.Paused and not DownloadStatus.Canceled)
                    live.Status = p.Status;
            }

            if (Stopwatch.GetTimestamp() - lastPersist > Stopwatch.Frequency * 2)
            {
                lastPersist = Stopwatch.GetTimestamp();
                Persist(job);
            }

            var snapshot = Find(id);
            if (snapshot is not null) JobUpdated?.Invoke(snapshot, p);
        });

        try
        {
            await downloader.RunAsync(job, progress, linked.Token).ConfigureAwait(false);
            _speeds[id] = 0;
            Persist(job);
            JobUpdated?.Invoke(JobStore.Clone(job), null);
            Toast?.Invoke($"완료: {job.FileName}");
        }
        catch (OperationCanceledException)
        {
            _speeds[id] = 0;
            lock (_jobsGate)
            {
                if (job.Status == DownloadStatus.Canceled)
                    TryDeleteTemp(job);
            }
            Persist(job);
            JobUpdated?.Invoke(JobStore.Clone(job), null);
        }
        catch (Exception ex)
        {
            _speeds[id] = 0;
            job.Status = DownloadStatus.Failed;
            job.ErrorMessage = ex.Message;
            Persist(job);
            JobUpdated?.Invoke(JobStore.Clone(job), null);
            Toast?.Invoke($"실패: {job.FileName}");
        }
        finally
        {
            _running.TryRemove(id, out _);
            cts.Dispose();
        }
    }

    private void Update(Guid id, Action<DownloadJob> mutate)
    {
        DownloadJob? clone = null;
        lock (_jobsGate)
        {
            var job = _jobs.FirstOrDefault(j => j.Id == id);
            if (job is null) return;
            mutate(job);
            Persist(job);
            clone = JobStore.Clone(job);
        }
        if (clone is not null) JobUpdated?.Invoke(clone, null);
    }

    private void Persist(DownloadJob job)
    {
        _store.Upsert(job);
    }

    private static void TryDeleteTemp(DownloadJob job)
    {
        try { if (File.Exists(job.TempPath)) File.Delete(job.TempPath); }
        catch { /* ignore */ }
    }

    public static string ResolveDirectory(AppSettings settings, string category, string? overrideDir)
    {
        if (!string.IsNullOrWhiteSpace(overrideDir))
            return overrideDir;

        if (settings.CategoryFolders.TryGetValue(category, out var specific) &&
            !string.IsNullOrWhiteSpace(specific))
            return specific;

        var root = string.IsNullOrWhiteSpace(settings.DefaultDownloadFolder)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")
            : settings.DefaultDownloadFolder;

        return settings.UseCategorySubfolders
            ? Path.Combine(root, CategoryClassifier.SubfolderName(category))
            : root;
    }

    public void Dispose()
    {
        _loopCts.Cancel();
        foreach (var cts in _running.Values)
        {
            try { cts.Cancel(); } catch { /* ignore */ }
        }
        try { _loop?.Wait(TimeSpan.FromSeconds(2)); } catch { /* ignore */ }
        _loopCts.Dispose();
        _persistLock.Dispose();
    }
}

public sealed class DownloadRequest
{
    public required string Url { get; init; }
    public string? FileName { get; init; }
    public string? SaveDirectory { get; init; }
    public string? Referrer { get; init; }
    public string? Cookies { get; init; }
    public string? UserAgent { get; init; }
    public string? Mime { get; init; }
    public Dictionary<string, string>? Headers { get; init; }
    public int? Connections { get; init; }
    public long? FileSize { get; init; }
    public bool Paused { get; init; }
    public bool OpenFolderWhenDone { get; init; }
    public DateTime? ScheduledAt { get; init; }
}
