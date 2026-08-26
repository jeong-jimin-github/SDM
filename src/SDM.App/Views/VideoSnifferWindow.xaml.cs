using System.Windows;
using System.Windows.Input;
using SDM.Core.Engine;
using SDM.Core.Ipc;
using SDM.Core.Models;

namespace SDM.App.Views;

public partial class VideoSnifferWindow : Window
{
    private readonly MediaSnifferStore _store;
    private readonly DownloadManager _manager;
    private readonly AppSettings _settings;

    public VideoSnifferWindow(MediaSnifferStore store, DownloadManager manager, AppSettings settings)
    {
        _store = store;
        _manager = manager;
        _settings = settings;
        InitializeComponent();
        _store.Changed += Refresh;
        Closed += (_, _) => _store.Changed -= Refresh;
        Refresh();
    }

    private void Refresh()
    {
        Dispatcher.Invoke(() =>
        {
            List.ItemsSource = _store.Snapshot().Select(h => new Row(h)).ToList();
        });
    }

    private void Clear_Click(object sender, RoutedEventArgs e) => _store.Clear();

    private void List_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (List.SelectedItem is not Row row) return;
        var dlg = new AddUrlWindow(_manager, _settings, new IpcMessage
        {
            Type = "add",
            Url = row.Hit.Url,
            Mime = row.Hit.Mime,
            FileSize = row.Hit.Size,
            PageUrl = row.Hit.PageUrl,
            Referrer = row.Hit.PageUrl
        }) { Owner = this };
        dlg.ShowDialog();
    }

    private sealed class Row
    {
        public Row(MediaHit hit)
        {
            Hit = hit;
            UrlShort = hit.Url.Length > 80 ? hit.Url[..80] + "…" : hit.Url;
            Mime = hit.Mime ?? "";
            PageTitle = hit.PageTitle ?? hit.PageUrl ?? "";
        }

        public MediaHit Hit { get; }
        public string UrlShort { get; }
        public string Mime { get; }
        public string PageTitle { get; }
    }
}
