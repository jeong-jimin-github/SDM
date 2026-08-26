namespace SDM.Core.Engine;

public sealed class SpeedLimiter
{
    private readonly object _gate = new();
    private long _bytesPerSecond;
    private double _tokens;
    private long _lastTicks = DateTime.UtcNow.Ticks;

    public long BytesPerSecond
    {
        get { lock (_gate) return _bytesPerSecond; }
        set { lock (_gate) _bytesPerSecond = Math.Max(0, value); }
    }

    public async Task ConsumeAsync(int bytes, CancellationToken ct)
    {
        while (true)
        {
            int waitMs;
            lock (_gate)
            {
                if (_bytesPerSecond <= 0) return;
                Refill();
                if (_tokens >= bytes)
                {
                    _tokens -= bytes;
                    return;
                }

                var need = bytes - _tokens;
                waitMs = (int)Math.Clamp(need * 1000.0 / _bytesPerSecond, 8, 250);
            }

            await Task.Delay(waitMs, ct).ConfigureAwait(false);
        }
    }

    private void Refill()
    {
        var now = DateTime.UtcNow.Ticks;
        var elapsed = (now - _lastTicks) / (double)TimeSpan.TicksPerSecond;
        _lastTicks = now;
        _tokens = Math.Min(_bytesPerSecond * 2, _tokens + elapsed * _bytesPerSecond);
    }
}
