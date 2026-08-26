using SDM.Core.Models;

namespace SDM.Core.Persistence;

public sealed class JobStore
{
    private readonly object _gate = new();
    private List<DownloadJob> _jobs = [];

    public IReadOnlyList<DownloadJob> Snapshot()
    {
        lock (_gate) return _jobs.Select(Clone).ToList();
    }

    public void Load()
    {
        AppPaths.EnsureCreated();
        lock (_gate)
            _jobs = JsonFile.LoadOrCreate(AppPaths.JobsFile, () => new List<DownloadJob>());
    }

    public void ReplaceAll(IEnumerable<DownloadJob> jobs)
    {
        lock (_gate)
        {
            _jobs = jobs.Select(Clone).ToList();
            Persist();
        }
    }

    public void Upsert(DownloadJob job)
    {
        lock (_gate)
        {
            var copy = Clone(job);
            var idx = _jobs.FindIndex(j => j.Id == job.Id);
            if (idx >= 0) _jobs[idx] = copy;
            else _jobs.Insert(0, copy);
            Persist();
        }
    }

    public void Remove(Guid id)
    {
        lock (_gate)
        {
            _jobs.RemoveAll(j => j.Id == id);
            Persist();
        }
    }

    private void Persist() => JsonFile.Save(AppPaths.JobsFile, _jobs);

    public static DownloadJob Clone(DownloadJob job) => new()
    {
        Id = job.Id,
        Url = job.Url,
        FileName = job.FileName,
        SaveDirectory = job.SaveDirectory,
        Referrer = job.Referrer,
        Cookies = job.Cookies,
        UserAgent = job.UserAgent,
        Mime = job.Mime,
        Headers = new Dictionary<string, string>(job.Headers, StringComparer.OrdinalIgnoreCase),
        TotalBytes = job.TotalBytes,
        DownloadedBytes = job.DownloadedBytes,
        Connections = job.Connections,
        SupportsRanges = job.SupportsRanges,
        Status = job.Status,
        Category = job.Category,
        CreatedAt = job.CreatedAt,
        StartedAt = job.StartedAt,
        CompletedAt = job.CompletedAt,
        ScheduledAt = job.ScheduledAt,
        ErrorMessage = job.ErrorMessage,
        Segments = job.Segments.Select(s => new SegmentState
        {
            Index = s.Index,
            Start = s.Start,
            End = s.End,
            Written = s.Written
        }).ToList(),
        FinalUrl = job.FinalUrl,
        OpenFolderWhenDone = job.OpenFolderWhenDone
    };
}
