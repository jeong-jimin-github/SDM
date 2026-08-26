using SDM.Core.Ipc;

namespace SDM.Core.Engine;

public sealed class MediaSnifferStore
{
    private readonly object _gate = new();
    private readonly List<MediaHit> _items = [];
    private const int MaxItems = 400;

    public event Action? Changed;

    public IReadOnlyList<MediaHit> Snapshot()
    {
        lock (_gate) return _items.ToList();
    }

    public void AddRange(IEnumerable<MediaHit> hits)
    {
        var added = false;
        lock (_gate)
        {
            foreach (var hit in hits)
            {
                if (string.IsNullOrWhiteSpace(hit.Url)) continue;
                if (_items.Any(x => string.Equals(x.Url, hit.Url, StringComparison.OrdinalIgnoreCase)))
                    continue;
                _items.Insert(0, hit);
                added = true;
            }

            if (_items.Count > MaxItems)
                _items.RemoveRange(MaxItems, _items.Count - MaxItems);
        }

        if (added) Changed?.Invoke();
    }

    public void Clear()
    {
        lock (_gate) _items.Clear();
        Changed?.Invoke();
    }
}
