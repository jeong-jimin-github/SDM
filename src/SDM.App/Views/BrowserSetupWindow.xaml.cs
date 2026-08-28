using System.Collections.Specialized;
using System.Windows;
using System.Windows.Input;
using SDM.App.Services;
using SDM.Core.Models;

namespace SDM.App.Views;

public partial class BrowserSetupWindow : Window
{
    private readonly AppSettings _settings;
    private Point _chromeDragStart;
    private Point _firefoxDragStart;
    private bool _chromeDragPending;
    private bool _firefoxDragPending;

    public BrowserSetupWindow(AppSettings settings)
    {
        _settings = settings;
        InitializeComponent();
        Refresh();
    }

    private void Install_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            BrowserIntegration.Install(_settings);
            Refresh();
            Clipboard.SetText(BrowserIntegration.Query().ChromePath);
            try
            {
                BrowserIntegration.OpenChromeExtensionsPage();
                StatusText.Text +=
                    "\n확장 관리 페이지를 열었습니다. 개발자 모드를 켠 뒤 왼쪽 카드를 그 창으로 끌어다 놓으세요.";
            }
            catch (Exception ex)
            {
                Clipboard.SetText("chrome://extensions");
                StatusText.Text +=
                    $"\n확장 페이지를 자동으로 열지 못했습니다 ({ex.Message}). 주소 chrome://extensions 를 클립보드에 복사했습니다.";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "SDM", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpenChromeExtPage_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            BrowserIntegration.OpenChromeExtensionsPage();
        }
        catch
        {
            Clipboard.SetText("chrome://extensions");
            MessageBox.Show(this, "주소 chrome://extensions 를 클립보드에 복사했습니다. 주소창에 붙여넣으세요.",
                "SDM", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void OpenFirefoxDebug_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            BrowserIntegration.OpenFirefoxDebugPage();
        }
        catch
        {
            Clipboard.SetText("about:debugging#/runtime/this-firefox");
            MessageBox.Show(this, "Firefox 디버깅 주소를 클립보드에 복사했습니다.", "SDM",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void ChromeDragCard_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _chromeDragStart = e.GetPosition(this);
        _chromeDragPending = true;
    }

    private void ChromeDragCard_OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!ShouldStartDrag(e, ref _chromeDragPending, _chromeDragStart)) return;
        try { StartExtensionDrag(EnsureChromeFolder()); }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "SDM", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void FirefoxDragCard_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _firefoxDragStart = e.GetPosition(this);
        _firefoxDragPending = true;
    }

    private void FirefoxDragCard_OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!ShouldStartDrag(e, ref _firefoxDragPending, _firefoxDragStart)) return;
        try { StartExtensionDrag(EnsureFirefoxDropPath()); }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "SDM", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private bool ShouldStartDrag(MouseEventArgs e, ref bool pending, Point origin)
    {
        if (!pending || e.LeftButton != MouseButtonState.Pressed)
        {
            if (e.LeftButton != MouseButtonState.Pressed) pending = false;
            return false;
        }

        var pos = e.GetPosition(this);
        if (Math.Abs(pos.X - origin.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(pos.Y - origin.Y) < SystemParameters.MinimumVerticalDragDistance)
            return false;

        pending = false;
        return true;
    }

    private string EnsureChromeFolder()
    {
        var path = BrowserIntegration.Query().ChromePath;
        if (!File.Exists(Path.Combine(path, "manifest.json")))
            BrowserIntegration.Install(_settings);
        Refresh();
        return BrowserIntegration.Query().ChromePath;
    }

    private string EnsureFirefoxDropPath()
    {
        var folder = BrowserIntegration.Query().FirefoxPath;
        if (!File.Exists(Path.Combine(folder, "manifest.json")))
            BrowserIntegration.Install(_settings);
        Refresh();
        var manifest = Path.Combine(BrowserIntegration.Query().FirefoxPath, "manifest.json");
        return File.Exists(manifest) ? manifest : BrowserIntegration.Query().FirefoxPath;
    }

    private void StartExtensionDrag(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || (!Directory.Exists(path) && !File.Exists(path)))
        {
            MessageBox.Show(this, "먼저 연결 파일 준비를 눌러 확장 파일을 만드세요.", "SDM",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var files = new StringCollection { path };
        var data = new DataObject();
        data.SetFileDropList(files);
        DragDrop.DoDragDrop(this, data, DragDropEffects.Copy);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Refresh()
    {
        var s = BrowserIntegration.Query();
        StatusText.Text =
            $"Native Host: {(s.NativeHostRegistered ? "등록됨" : "미등록")}\n" +
            $"sdm:// 프로토콜: {(s.ProtocolRegistered ? "등록됨" : "미등록")}\n" +
            $"Native Host 경로: {s.NativeHostPath}";
        PathHint.Text = $"Chrome 확장: {s.ChromePath}\nFirefox 확장: {s.FirefoxPath}";
        ChromeDragPath.Text = string.IsNullOrWhiteSpace(s.ChromePath) ? "연결 파일 준비 후 경로가 표시됩니다." : s.ChromePath;
        FirefoxDragPath.Text = string.IsNullOrWhiteSpace(s.FirefoxPath) ? "연결 파일 준비 후 경로가 표시됩니다." : s.FirefoxPath;
    }
}
