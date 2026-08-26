using System.Windows;
using SDM.Core.Engine;
using SDM.Core.Ipc;
using SDM.Core.Models;

namespace SDM.App.Views;

public partial class AddUrlWindow : Window
{
    private readonly DownloadManager _manager;
    private readonly AppSettings _settings;
    private readonly IpcMessage? _seed;
    private bool _userEditedName;
    private string _lastAutoFolder = "";
    private string? _resolvedMime;

    public Guid? CreatedJobId { get; private set; }

    public AddUrlWindow(DownloadManager manager, AppSettings settings, IpcMessage? seed)
    {
        _manager = manager;
        _settings = settings;
        _seed = seed;
        InitializeComponent();
        ConnSlider.Value = settings.DefaultConnections;
        _resolvedMime = seed?.Mime;

        var url = seed?.Url ?? "";
        var name = FileNameHelper.Resolve(seed?.Filename, url, mime: seed?.Mime);
        UrlBox.Text = url;
        NameBox.Text = name;
        ApplySummary(name, url, seed?.FileSize, seed?.Mime, probing: true);
        UrlBox.TextChanged += (_, _) => RefreshHint();
        NameBox.TextChanged += (_, _) =>
        {
            _userEditedName = true;
            RefreshHint();
        };
        RefreshFolder();
        RefreshHint();
        Loaded += async (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(UrlBox.Text)) UrlBox.Focus();
            else NameBox.Focus();
            await ProbeHeadersAsync();
        };
    }

    private async Task ProbeHeadersAsync()
    {
        var url = UrlBox.Text.Trim();
        if (!FileNameHelper.LooksLikeUrl(url))
        {
            ApplySummary(NameBox.Text, url, _seed?.FileSize, _resolvedMime, probing: false);
            return;
        }

        try
        {
            var job = new DownloadJob
            {
                Url = url,
                FileName = NameBox.Text,
                Referrer = _seed?.Referrer,
                Cookies = _seed?.Cookies,
                UserAgent = _seed?.UserAgent,
                Mime = _resolvedMime,
                Headers = _seed?.Headers ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            };
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(12));
            var probe = await FileProbe.ProbeAsync(job, cts.Token).ConfigureAwait(true);
            if (probe.TotalBytes > 0 && _seed is not null)
                _seed.FileSize = probe.TotalBytes;
            if (!string.IsNullOrWhiteSpace(probe.Mime))
                _resolvedMime = probe.Mime;
            if (!_userEditedName && !string.IsNullOrWhiteSpace(probe.FileName))
                NameBox.Text = probe.FileName;
            ApplySummary(NameBox.Text, probe.FinalUrl, probe.TotalBytes > 0 ? probe.TotalBytes : _seed?.FileSize,
                _resolvedMime, probing: false);
            RefreshHint();
        }
        catch
        {
            ApplySummary(NameBox.Text, url, _seed?.FileSize, _resolvedMime, probing: false);
        }
    }

    private void ApplySummary(string name, string url, long? size, string? mime, bool probing)
    {
        SummaryName.Text = string.IsNullOrWhiteSpace(name) ? (probing ? "파일 정보를 읽는 중…" : "이름 없음") : name;
        var bits = new List<string>();
        if (size is > 0) bits.Add(ByteFormatter.Bytes(size.Value));
        if (!string.IsNullOrWhiteSpace(mime)) bits.Add(mime);
        if (!string.IsNullOrWhiteSpace(url)) bits.Add(url.Length > 90 ? url[..90] + "…" : url);
        SummaryMeta.Text = bits.Count == 0
            ? (probing ? "서버 응답을 확인하는 중입니다." : "")
            : string.Join("  ·  ", bits);
    }

    private void ConnSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ConnLabel is not null)
            ConnLabel.Text = ((int)e.NewValue).ToString();
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "저장 폴더",
            FolderName = FolderBox.Text
        };
        if (dlg.ShowDialog() == true)
            FolderBox.Text = dlg.FolderName;
    }

    private void RefreshHint()
    {
        var name = string.IsNullOrWhiteSpace(NameBox.Text)
            ? FileNameHelper.FromUrl(UrlBox.Text)
            : NameBox.Text;
        var cat = CategoryClassifier.FromFileName(name, _resolvedMime ?? _seed?.Mime);
        var title = CategoryClassifier.All.FirstOrDefault(c => c.Id == cat).TitleKo;
        CategoryHint.Text = $"분류: {title}";
        if (string.IsNullOrWhiteSpace(FolderBox.Text) || FolderBox.Text == _lastAutoFolder)
            RefreshFolder(cat);
        if (!string.IsNullOrWhiteSpace(SummaryName.Text) && SummaryName.Text != "파일 정보를 읽는 중…")
            ApplySummary(name, UrlBox.Text, _seed?.FileSize, _resolvedMime, probing: false);
    }

    private void RefreshFolder(string? category = null)
    {
        category ??= CategoryClassifier.FromFileName(NameBox.Text, _resolvedMime ?? _seed?.Mime);
        _lastAutoFolder = DownloadManager.ResolveDirectory(_settings, category, null);
        FolderBox.Text = _lastAutoFolder;
    }

    private void Start_Click(object sender, RoutedEventArgs e) => Commit(paused: false);
    private void Queue_Click(object sender, RoutedEventArgs e) => Commit(paused: true);
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Commit(bool paused)
    {
        var url = UrlBox.Text.Trim();
        if (!FileNameHelper.LooksLikeUrl(url))
        {
            MessageBox.Show(this, "유효한 http(s) 주소를 입력하세요.", "SDM",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var job = _manager.Enqueue(new DownloadRequest
        {
            Url = url,
            FileName = string.IsNullOrWhiteSpace(NameBox.Text) ? null : NameBox.Text.Trim(),
            SaveDirectory = FolderBox.Text.Trim(),
            Referrer = _seed?.Referrer,
            Cookies = _seed?.Cookies,
            UserAgent = _seed?.UserAgent,
            Mime = _resolvedMime ?? _seed?.Mime,
            Headers = _seed?.Headers,
            FileSize = _seed?.FileSize,
            Connections = (int)ConnSlider.Value,
            Paused = paused
        });

        CreatedJobId = job.Id;
        DialogResult = true;
    }
}
