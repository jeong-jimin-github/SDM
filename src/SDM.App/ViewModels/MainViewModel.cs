using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Windows;
using System.Windows.Data;
using SDM.Core.Engine;
using SDM.Core.Models;
using SDM.Core.Persistence;

namespace SDM.App.ViewModels;

public sealed class CategoryItem : ObservableObject
{
    private int _count;
    private bool _selected;
    public required string Id { get; init; }
    public required string Title { get; init; }
    public int Count { get => _count; set => SetProperty(ref _count, value); }
    public bool IsSelected { get => _selected; set => SetProperty(ref _selected, value); }
}

public sealed class MainViewModel : ObservableObject, IDisposable
{
    private readonly DownloadManager _manager;
    private readonly object _itemsLock = new();
    private string _filter = CategoryClassifier.General;
    private string _search = "";
    private DownloadItemViewModel? _selected;
    private string _statusLine = "준비됨";
    private string _speedLine = "0 B/s";
    private string _browserLine = "브라우저 미연결";
    private bool _browserConnected;
    private readonly List<double> _speedHistory = [];

    public MainViewModel(DownloadManager manager)
    {
        _manager = manager;
        BindingOperations.EnableCollectionSynchronization(Items, _itemsLock);

        foreach (var (id, title) in CategoryClassifier.All)
            Categories.Add(new CategoryItem { Id = id, Title = title });

        AddCommand = new RelayCommand(OnAdd);
        PauseCommand = new RelayCommand(() => WithSelected(j => _manager.Pause(j.Id)), () => Selected?.CanPause == true);
        ResumeCommand = new RelayCommand(() => WithSelected(j => _manager.Resume(j.Id)), () => Selected?.CanResume == true);
        CancelCommand = new RelayCommand(() => WithSelected(j => _manager.Cancel(j.Id)), () => Selected is { IsCompleted: false });
        RemoveCommand = new RelayCommand(OnRemove, () => Selected is not null);
        OpenFolderCommand = new RelayCommand(OnOpenFolder, () => Selected is not null);
        PauseAllCommand = new RelayCommand(() => _manager.PauseAll());
        ResumeAllCommand = new RelayCommand(() => _manager.ResumeAll());
        SettingsCommand = new RelayCommand(() => RequestSettings?.Invoke());
        BrowserCommand = new RelayCommand(() => RequestBrowser?.Invoke());
        SnifferCommand = new RelayCommand(() => RequestSniffer?.Invoke());
        ClearSearchCommand = new RelayCommand(() => Search = "", () => !string.IsNullOrEmpty(Search));
        FilterCommand = new RelayCommand(p =>
        {
            if (p is string id) Filter = id;
        });
        SyncCategorySelection();

        _manager.JobAdded += job => Dispatch(() =>
        {
            var vm = new DownloadItemViewModel();
            vm.Apply(job, null);
            lock (_itemsLock) Items.Insert(0, vm);
            RefreshCounts();
            ApplyFilter();
        });
        _manager.JobUpdated += (job, progress) => Dispatch(() =>
        {
            DownloadItemViewModel? vm;
            lock (_itemsLock) vm = Items.FirstOrDefault(x => x.Id == job.Id);
            if (vm is null)
            {
                vm = new DownloadItemViewModel();
                vm.Apply(job, progress);
                lock (_itemsLock) Items.Insert(0, vm);
            }
            else vm.Apply(job, progress);
            RefreshCounts();
            RefreshTotals();
            ApplyFilter();
            RelayCommand.RaiseCanExecuteChanged();
        });
        _manager.JobRemoved += id => Dispatch(() =>
        {
            lock (_itemsLock)
            {
                var vm = Items.FirstOrDefault(x => x.Id == id);
                if (vm is not null) Items.Remove(vm);
            }
            if (Selected?.Id == id) Selected = null;
            RefreshCounts();
        });
        _manager.Toast += msg => Dispatch(() => RequestToast?.Invoke(msg));

        Items.CollectionChanged += OnCollectionChanged;

        foreach (var job in _manager.Jobs)
        {
            var vm = new DownloadItemViewModel();
            vm.Apply(job, null);
            Items.Add(vm);
        }

        RefreshCounts();
        ApplyFilter();
        RefreshTotals();
    }

    public ObservableCollection<DownloadItemViewModel> Items { get; } = [];
    public ObservableCollection<DownloadItemViewModel> VisibleItems { get; } = [];
    public ObservableCollection<CategoryItem> Categories { get; } = [];
    public IReadOnlyList<double> SpeedHistory => _speedHistory;

    public string Filter
    {
        get => _filter;
        set
        {
            if (SetProperty(ref _filter, value))
            {
                SyncCategorySelection();
                ApplyFilter();
            }
        }
    }

    public string Search
    {
        get => _search;
        set
        {
            if (SetProperty(ref _search, value ?? ""))
            {
                RelayCommand.RaiseCanExecuteChanged();
                ApplyFilter();
            }
        }
    }

    public DownloadItemViewModel? Selected
    {
        get => _selected;
        set
        {
            if (SetProperty(ref _selected, value))
                RelayCommand.RaiseCanExecuteChanged();
        }
    }

    public string StatusLine { get => _statusLine; set => SetProperty(ref _statusLine, value); }
    public string SpeedLine { get => _speedLine; set => SetProperty(ref _speedLine, value); }
    public string BrowserLine { get => _browserLine; set => SetProperty(ref _browserLine, value); }
    public bool BrowserConnected { get => _browserConnected; set => SetProperty(ref _browserConnected, value); }

    public RelayCommand AddCommand { get; }
    public RelayCommand PauseCommand { get; }
    public RelayCommand ResumeCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand RemoveCommand { get; }
    public RelayCommand OpenFolderCommand { get; }
    public RelayCommand PauseAllCommand { get; }
    public RelayCommand ResumeAllCommand { get; }
    public RelayCommand SettingsCommand { get; }
    public RelayCommand BrowserCommand { get; }
    public RelayCommand SnifferCommand { get; }
    public RelayCommand ClearSearchCommand { get; }
    public RelayCommand FilterCommand { get; }

    public event Action? RequestAdd;
    public event Action? RequestSettings;
    public event Action? RequestBrowser;
    public event Action? RequestSniffer;
    public event Action<string>? RequestToast;
    public event Action? SpeedSampled;

    public DownloadManager Manager => _manager;

    public void TickSpeed()
    {
        var bps = _manager.TotalBytesPerSecond;
        SpeedLine = ByteFormatter.Speed(bps);
        _speedHistory.Add(bps);
        if (_speedHistory.Count > 60) _speedHistory.RemoveAt(0);
        var active = Items.Count(i => i.IsActive);
        var queued = Items.Count(i => i.Status == DownloadStatus.Queued);
        StatusLine = active > 0
            ? $"{active}개 받는 중 · 대기 {queued}개"
            : queued > 0 ? $"대기 {queued}개" : "준비됨";
        SpeedSampled?.Invoke();
    }

    public void MarkBrowser(string? browser)
    {
        BrowserConnected = true;
        BrowserLine = string.IsNullOrWhiteSpace(browser) ? "브라우저 연결됨" : $"{browser} 연결됨";
    }

    private void OnAdd() => RequestAdd?.Invoke();

    private void OnRemove()
    {
        if (Selected is null) return;
        var delete = Selected.IsCompleted &&
                     MessageBox.Show("완료된 항목을 목록에서 지울까요?\n파일을 휴지통이 아닌 목록에서만 제거합니다.",
                         AppPaths.ProductName, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
        if (!Selected.IsCompleted || delete)
            _manager.Remove(Selected.Id, deleteFile: false);
    }

    private void OnOpenFolder()
    {
        if (Selected is null) return;
        var path = Selected.SavePath;
        if (File.Exists(path))
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
        else
        {
            var dir = Path.GetDirectoryName(path);
            if (dir is not null) Process.Start(new ProcessStartInfo("explorer.exe", dir) { UseShellExecute = true });
        }
    }

    private void WithSelected(Action<DownloadItemViewModel> action)
    {
        if (Selected is not null) action(Selected);
    }

    private void SyncCategorySelection()
    {
        foreach (var cat in Categories)
            cat.IsSelected = string.Equals(cat.Id, _filter, StringComparison.OrdinalIgnoreCase);
    }

    private void ApplyFilter()
    {
        IEnumerable<DownloadItemViewModel> q = Items;
        if (!string.Equals(Filter, CategoryClassifier.General, StringComparison.OrdinalIgnoreCase))
            q = q.Where(i => i.Category == Filter);
        if (!string.IsNullOrWhiteSpace(Search))
            q = q.Where(i => i.FileName.Contains(Search, StringComparison.OrdinalIgnoreCase)
                             || i.Url.Contains(Search, StringComparison.OrdinalIgnoreCase));

        VisibleItems.Clear();
        foreach (var item in q) VisibleItems.Add(item);
        OnPropertyChanged(nameof(VisibleItems));
        OnPropertyChanged(nameof(HasVisibleItems));
        OnPropertyChanged(nameof(ResultLine));
    }

    public bool HasVisibleItems => VisibleItems.Count > 0;
    public string ResultLine => string.IsNullOrWhiteSpace(Search)
        ? $"{VisibleItems.Count}개 항목"
        : $"“{Search.Trim()}” 검색 결과 {VisibleItems.Count}개";

    private void RefreshCounts()
    {
        foreach (var cat in Categories)
            cat.Count = cat.Id == CategoryClassifier.General
                ? Items.Count
                : Items.Count(i => i.Category == cat.Id);
        ApplyFilter();
    }

    private void RefreshTotals()
    {
        // counts already cover this
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => RefreshCounts();

    private static void Dispatch(Action action)
    {
        var disp = Application.Current?.Dispatcher;
        if (disp is null || disp.CheckAccess()) action();
        else disp.Invoke(action);
    }

    public void Dispose()
    {
        Items.CollectionChanged -= OnCollectionChanged;
    }
}
