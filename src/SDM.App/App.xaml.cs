using System.Threading;
using System.Windows;
using System.Windows.Threading;
using SDM.App.Services;
using SDM.Core.Ipc;
using SDM.Core.Persistence;

namespace SDM.App;

public partial class App : Application
{
    private Mutex? _mutex;
    private AppHost? _host;
    private ClipboardMonitor? _clipboard;
    private MainWindow? _window;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _mutex = new Mutex(true, AppPaths.MutexName, out var created);
        if (!created)
        {
            await ForwardToRunningInstanceAsync(e.Args);
            Shutdown();
            return;
        }

        AppPaths.EnsureCreated();
        _host = new AppHost();
        try { BrowserIntegration.Install(_host.SettingsStore.Current); }
        catch { /* registry/extension copy is best-effort */ }
        _host.HandleStartupArgs(e.Args);

        _window = new MainWindow(_host);
        MainWindow = _window;
        _window.Show();

        _clipboard = new ClipboardMonitor(_host.SettingsStore.Current);
        _clipboard.UrlDetected += url =>
        {
            if (_window is null) return;
            _window.Dispatcher.Invoke(() =>
            {
                _window.Show();
                _window.Activate();
                _window.ShowAdd(new IpcMessage { Type = "add", Url = url });
            });
        };

        var tick = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        tick.Tick += (_, _) => _host.Main.TickSpeed();
        tick.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _clipboard?.Dispose();
        _host?.Dispose();
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }

    private static async Task ForwardToRunningInstanceAsync(string[] args)
    {
        try
        {
            var url = args.Select(AppHost.ExtractUrl).FirstOrDefault(u => u is not null);
            var msg = url is null
                ? new IpcMessage { Type = "open" }
                : new IpcMessage { Type = "add", Url = url, Confirm = true };
            await NamedPipeHub.SendAsync(msg, TimeSpan.FromSeconds(3));
        }
        catch
        {
            // running instance not reachable
        }
    }
}
