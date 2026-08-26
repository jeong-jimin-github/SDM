using System.Windows;
using SDM.Core.Persistence;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace SDM.App.Services;

public sealed class TrayService : IDisposable
{
    private readonly Forms.NotifyIcon _icon;

    public TrayService(Window window)
    {
        _icon = new Forms.NotifyIcon
        {
            Visible = true,
            Text = AppPaths.ProductName,
            Icon = LoadIcon()
        };
        _icon.DoubleClick += (_, _) => Show(window);
        _icon.ContextMenuStrip = new Forms.ContextMenuStrip();
        _icon.ContextMenuStrip.Items.Add("열기", null, (_, _) => Show(window));
        _icon.ContextMenuStrip.Items.Add("종료", null, (_, _) =>
        {
            _icon.Visible = false;
            Application.Current.Shutdown();
        });
    }

    public void Balloon(string text)
    {
        try
        {
            _icon.BalloonTipTitle = AppPaths.ProductName;
            _icon.BalloonTipText = text;
            _icon.ShowBalloonTip(2500);
        }
        catch { /* ignore */ }
    }

    private static void Show(Window window)
    {
        window.Show();
        window.WindowState = WindowState.Normal;
        window.Activate();
    }

    private static Drawing.Icon LoadIcon()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "sdm.ico");
        if (File.Exists(path)) return new Drawing.Icon(path);
        var packed = Path.Combine(AppContext.BaseDirectory, "sdm.ico");
        if (File.Exists(packed)) return new Drawing.Icon(packed);
        return Drawing.SystemIcons.Application;
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
