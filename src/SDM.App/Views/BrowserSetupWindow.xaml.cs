using System.Diagnostics;
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
            MessageBox.Show(this, "Native Messaging 호스트와 프로토콜이 등록되었습니다.\n이어서 브라우저에 확장을 로드하세요.",
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
            Process.Start(new ProcessStartInfo("http://chrome://extensions") { UseShellExecute = true });
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
            Process.Start(new ProcessStartInfo("firefox.exe", "about:debugging#/runtime/this-firefox") { UseShellExecute = true });
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
