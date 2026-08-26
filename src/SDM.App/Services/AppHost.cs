using SDM.Core.Engine;
using SDM.Core.Ipc;
using SDM.Core.Models;
using SDM.Core.Persistence;
using SDM.App.ViewModels;
using SDM.App.Views;
using System.Windows;

namespace SDM.App.Services;

public sealed class AppHost : IDisposable
{
    public SettingsStore SettingsStore { get; } = new();
    public DownloadManager Manager { get; }
    public MediaSnifferStore Sniffer { get; } = new();
    public MainViewModel Main { get; }

    private NamedPipeHub? _pipe;
    private LoopbackServer? _http;
    private DateTime _lastBrowserSeen = DateTime.MinValue;

    public AppHost()
    {
        SettingsStore.Load();
        Manager = new DownloadManager(SettingsStore);
        Manager.Start();
        Main = new MainViewModel(Manager);
        _pipe = new NamedPipeHub(HandleAsync);
        _pipe.Start();
        try
        {
            _http = new LoopbackServer(SettingsStore.Current, HandleAsync);
            _http.Start();
        }
        catch
        {
            _http = null;
        }
    }

    public void HandleStartupArgs(string[] args)
    {
        foreach (var arg in args)
            TryHandleExternal(arg);
    }

    public bool TryHandleExternal(string raw)
    {
        var url = ExtractUrl(raw);
        if (url is null) return false;
        Application.Current.Dispatcher.Invoke(() => OfferDownload(new IpcMessage
        {
            Type = "add",
            Url = url
        }));
        return true;
    }

    private async Task<IpcMessage> HandleAsync(IpcMessage message)
    {
        switch (message.Type.ToLowerInvariant())
        {
            case "ping":
                NoteBrowser(message.Browser);
                return new IpcMessage
                {
                    Type = "pong",
                    Ok = true,
                    Version = AppPaths.Version,
                    Token = SettingsStore.Current.IpcToken
                };
            case "add":
                NoteBrowser(message.Browser);
                var add = await DispatchAsync(() => OfferDownload(message)).ConfigureAwait(false);
                return add;
            case "media":
                NoteBrowser(message.Browser);
                if (message.Media is { Count: > 0 })
                    Sniffer.AddRange(message.Media);
                else if (!string.IsNullOrWhiteSpace(message.Url))
                    Sniffer.AddRange([new MediaHit
                    {
                        Url = message.Url,
                        Mime = message.Mime,
                        Size = message.FileSize,
                        PageUrl = message.PageUrl,
                        PageTitle = message.PageTitle
                    }]);
                return new IpcMessage { Type = "ok", Ok = true };
            case "open":
                await DispatchAsync(() =>
                {
                    Application.Current.MainWindow?.Show();
                    Application.Current.MainWindow?.Activate();
                    return true;
                }).ConfigureAwait(false);
                return new IpcMessage { Type = "ok", Ok = true };
            default:
                return new IpcMessage { Type = "error", Ok = false, Error = $"unknown:{message.Type}" };
        }
    }

    private IpcMessage OfferDownload(IpcMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.Url))
            return new IpcMessage { Type = "error", Ok = false, Error = "url" };

        var confirm = message.Confirm ?? SettingsStore.Current.ConfirmBeforeAdd;
        if (confirm)
        {
            var dlg = new AddUrlWindow(Manager, SettingsStore.Current, message)
            {
                Owner = Application.Current.MainWindow
            };
            var ok = dlg.ShowDialog() == true;
            return new IpcMessage { Type = ok ? "ok" : "cancel", Ok = ok, JobId = dlg.CreatedJobId };
        }

        var job = Manager.Enqueue(new DownloadRequest
        {
            Url = message.Url,
            FileName = message.Filename,
            Referrer = message.Referrer,
            Cookies = message.Cookies,
            UserAgent = message.UserAgent,
            Mime = message.Mime,
            Headers = message.Headers,
            FileSize = message.FileSize,
            Connections = message.Connections,
            SaveDirectory = message.SavePath
        });
        return new IpcMessage { Type = "ok", Ok = true, JobId = job.Id };
    }

    private void NoteBrowser(string? browser)
    {
        _lastBrowserSeen = DateTime.Now;
        var disp = Application.Current?.Dispatcher;
        disp?.BeginInvoke(() => Main.MarkBrowser(browser));
    }

    private static Task<T> DispatchAsync<T>(Func<T> fn)
    {
        var disp = Application.Current.Dispatcher;
        return disp.CheckAccess() ? Task.FromResult(fn()) : disp.InvokeAsync(fn).Task;
    }

    public static string? ExtractUrl(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        raw = raw.Trim().Trim('"');
        if (raw.StartsWith("sdm:", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var q = raw.IndexOf('?');
                if (q >= 0)
                {
                    foreach (var part in raw[(q + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries))
                    {
                        var kv = part.Split('=', 2);
                        if (kv.Length == 2 && kv[0].Equals("url", StringComparison.OrdinalIgnoreCase))
                            return Uri.UnescapeDataString(kv[1]);
                    }
                }
                return FileNameHelper.ExtractUrl(raw);
            }
            catch
            {
                return FileNameHelper.ExtractUrl(raw);
            }
        }
        if (raw is "--add" or "-a") return null;
        return FileNameHelper.ExtractUrl(raw);
    }

    public void Dispose()
    {
        _pipe?.Dispose();
        _http?.Dispose();
        Main.Dispose();
        Manager.Dispose();
    }
}
