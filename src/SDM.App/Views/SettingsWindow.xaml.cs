using System.Windows;
using SDM.Core.Persistence;

namespace SDM.App.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsStore _store;

    public SettingsWindow(SettingsStore store)
    {
        _store = store;
        InitializeComponent();
        var s = store.Current;
        FolderBox.Text = s.DefaultDownloadFolder;
        CategoryFoldersBox.IsChecked = s.UseCategorySubfolders;
        ConnBox.Text = s.DefaultConnections.ToString();
        ConcurrentBox.Text = s.MaxConcurrentDownloads.ToString();
        LimitBox.Text = (s.SpeedLimitBytesPerSecond / 1024).ToString();
        ClipboardBox.IsChecked = s.WatchClipboard;
        ConfirmBox.IsChecked = s.ConfirmBeforeAdd;
        ResumeBox.IsChecked = s.AutoResumeOnStart;
        TrayBox.IsChecked = s.CloseToTray;
        StartupBox.IsChecked = s.LaunchAtStartup;
        NotifyBox.IsChecked = s.ShowNotifications;
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "기본 저장 폴더", FolderName = FolderBox.Text };
        if (dlg.ShowDialog() == true) FolderBox.Text = dlg.FolderName;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var s = _store.Current;
        s.DefaultDownloadFolder = FolderBox.Text.Trim();
        s.UseCategorySubfolders = CategoryFoldersBox.IsChecked == true;
        if (int.TryParse(ConnBox.Text, out var c)) s.DefaultConnections = Math.Clamp(c, 1, 32);
        if (int.TryParse(ConcurrentBox.Text, out var m)) s.MaxConcurrentDownloads = Math.Clamp(m, 1, 16);
        if (long.TryParse(LimitBox.Text, out var kb)) s.SpeedLimitBytesPerSecond = Math.Max(0, kb) * 1024;
        s.WatchClipboard = ClipboardBox.IsChecked == true;
        s.ConfirmBeforeAdd = ConfirmBox.IsChecked == true;
        s.AutoResumeOnStart = ResumeBox.IsChecked == true;
        s.CloseToTray = TrayBox.IsChecked == true;
        s.LaunchAtStartup = StartupBox.IsChecked == true;
        s.ShowNotifications = NotifyBox.IsChecked == true;
        _store.Save();
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
