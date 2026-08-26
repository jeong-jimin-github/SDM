using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using SDM.App.Services;
using SDM.App.ViewModels;
using SDM.App.Views;
using SDM.Core.Persistence;

namespace SDM.App;

public partial class MainWindow : Window
{
    private readonly AppHost _host;
    private readonly TrayService _tray;

    public MainWindow(AppHost host)
    {
        _host = host;
        DataContext = host.Main;
        InitializeComponent();
        _tray = new TrayService(this);
        host.Main.RequestAdd += () => ShowAdd(null);
        host.Main.RequestSettings += ShowSettings;
        host.Main.RequestBrowser += ShowBrowser;
        host.Main.RequestSniffer += ShowSniffer;
        host.Main.RequestToast += msg =>
        {
            if (host.SettingsStore.Current.ShowNotifications)
                _tray.Balloon(msg);
        };
        host.Main.SpeedSampled += DrawSpeed;
        Closing += OnClosing;
        KeyDown += OnKeyDown;
    }

    public TrayService Tray => _tray;

    public void ShowAdd(Core.Ipc.IpcMessage? seed)
    {
        var dlg = new AddUrlWindow(_host.Manager, _host.SettingsStore.Current, seed) { Owner = this };
        dlg.ShowDialog();
    }

    private void ShowSettings()
    {
        var dlg = new SettingsWindow(_host.SettingsStore) { Owner = this };
        dlg.ShowDialog();
        _host.Manager.ApplySettings();
        BrowserIntegration.RegisterStartup(BrowserIntegration.LocateAppExe(), _host.SettingsStore.Current.LaunchAtStartup);
    }

    private void ShowBrowser()
    {
        var dlg = new BrowserSetupWindow(_host.SettingsStore.Current) { Owner = this };
        dlg.ShowDialog();
    }

    private void ShowSniffer()
    {
        var dlg = new VideoSnifferWindow(_host.Sniffer, _host.Manager, _host.SettingsStore.Current) { Owner = this };
        dlg.Show();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.N && Keyboard.Modifiers == ModifierKeys.Control) _host.Main.AddCommand.Execute(null);
        else if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control) _host.Main.SettingsCommand.Execute(null);
        else if (e.Key == Key.Delete) _host.Main.RemoveCommand.Execute(null);
        else if (e.Key == Key.Space)
        {
            if (_host.Main.Selected?.CanPause == true) _host.Main.PauseCommand.Execute(null);
            else _host.Main.ResumeCommand.Execute(null);
        }
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_host.SettingsStore.Current.CloseToTray)
        {
            e.Cancel = true;
            Hide();
        }
        else
        {
            _tray.Dispose();
        }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        if (_host.SettingsStore.Current.MinimizeToTray) Hide();
        else WindowState = WindowState.Minimized;
    }

    private void Maximize_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void DrawSpeed()
    {
        SpeedCanvas.Children.Clear();
        var hist = _host.Main.SpeedHistory;
        if (hist.Count < 2) return;
        var w = SpeedCanvas.ActualWidth;
        var h = SpeedCanvas.ActualHeight;
        if (w <= 0 || h <= 0) return;
        var max = Math.Max(1, hist.Max());
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            for (var i = 0; i < hist.Count; i++)
            {
                var x = i / (double)(hist.Count - 1) * w;
                var y = h - hist[i] / max * (h - 2) - 1;
                if (i == 0) ctx.BeginFigure(new Point(x, y), false, false);
                else ctx.LineTo(new Point(x, y), true, false);
            }
        }
        geo.Freeze();
        SpeedCanvas.Children.Add(new System.Windows.Shapes.Path
        {
            Data = geo,
            Stroke = new SolidColorBrush(Color.FromRgb(0x34, 0xD3, 0x99)),
            StrokeThickness = 1.4,
            SnapsToDevicePixels = true
        });
    }
}
