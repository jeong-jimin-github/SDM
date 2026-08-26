using System.Windows;
using System.Windows.Threading;
using SDM.Core.Engine;
using SDM.Core.Models;

namespace SDM.App.Services;

public sealed class ClipboardMonitor : IDisposable
{
    private readonly DispatcherTimer _timer;
    private readonly AppSettings _settings;
    private string? _last;

    public event Action<string>? UrlDetected;

    public ClipboardMonitor(AppSettings settings)
    {
        _settings = settings;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
        _timer.Tick += (_, _) => Poll();
        _timer.Start();
    }

    private void Poll()
    {
        if (!_settings.WatchClipboard) return;
        try
        {
            if (!Clipboard.ContainsText()) return;
            var text = Clipboard.GetText();
            if (_last is null)
            {
                _last = text;
                return;
            }
            if (text == _last) return;
            _last = text;
            var url = FileNameHelper.ExtractUrl(text);
            if (url is not null) UrlDetected?.Invoke(url);
        }
        catch
        {
            // clipboard can be locked by another process
        }
    }

    public void Dispose() => _timer.Stop();
}
