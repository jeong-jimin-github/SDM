using System.Windows;
using SDM.App.Services;
using SDM.Core.Models;

namespace SDM.App.Views;

public partial class BrowserSetupWindow : Window
{
    private readonly AppSettings _settings;

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
            var path = BrowserIntegration.Query().ChromePath;
            Clipboard.SetText(path);
            BrowserIntegration.OpenFolder(path);
            try { BrowserIntegration.OpenChromeExtensionsPage(); } catch { }
            MessageBox.Show(this, "연결 구성과 확장 파일 준비가 끝났습니다.\n\n확장 페이지에서 개발자 모드를 켠 뒤 ‘압축해제된 확장 프로그램을 로드합니다’를 누르세요. 선택할 폴더 경로는 클립보드에 복사했고 탐색기에도 열었습니다.",
                "SDM", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "SDM", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpenChrome_Click(object sender, RoutedEventArgs e) =>
        BrowserIntegration.OpenFolder(BrowserIntegration.Query().ChromePath);

    private void OpenFirefox_Click(object sender, RoutedEventArgs e) =>
        BrowserIntegration.OpenFolder(BrowserIntegration.Query().FirefoxPath);

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

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Refresh()
    {
        var s = BrowserIntegration.Query();
        StatusText.Text =
            $"Native Host: {(s.NativeHostRegistered ? "등록됨" : "미등록")}\n" +
            $"sdm:// 프로토콜: {(s.ProtocolRegistered ? "등록됨" : "미등록")}\n" +
            $"Native Host 경로: {s.NativeHostPath}";
        PathHint.Text = $"Chrome 확장: {s.ChromePath}\nFirefox 확장: {s.FirefoxPath}";
    }
}
