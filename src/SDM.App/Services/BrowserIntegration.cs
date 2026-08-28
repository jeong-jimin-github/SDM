using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using SDM.Core.Models;
using SDM.Core.Persistence;
using Microsoft.Win32;

namespace SDM.App.Services;

public sealed class BrowserIntegrationStatus
{
    public bool NativeHostRegistered { get; set; }
    public bool ProtocolRegistered { get; set; }
    public bool ChromeExtensionPresent { get; set; }
    public bool FirefoxExtensionPresent { get; set; }
    public string ChromePath { get; set; } = "";
    public string FirefoxPath { get; set; } = "";
    public string NativeHostPath { get; set; } = "";
}

public static class BrowserIntegration
{
    public static BrowserIntegrationStatus Install(AppSettings settings)
    {
        var appExe = LocateAppExe();
        var nativeHost = LocateNativeHost();
        var source = LocateBundledExtensions();

        Directory.CreateDirectory(AppPaths.NativeManifestDir);
        Directory.CreateDirectory(AppPaths.ChromeExtensionDir);
        Directory.CreateDirectory(AppPaths.FirefoxExtensionDir);

        CopyExtension(source, "chrome", AppPaths.ChromeExtensionDir, settings);
        CopyExtension(source, "firefox", AppPaths.FirefoxExtensionDir, settings);

        var chromeManifest = Path.Combine(AppPaths.NativeManifestDir, "com.sdm.host.chrome.json");
        var firefoxManifest = Path.Combine(AppPaths.NativeManifestDir, "com.sdm.host.firefox.json");

        File.WriteAllText(chromeManifest, JsonSerializer.Serialize(new
        {
            name = AppPaths.NativeHostName,
            description = "SDM native messaging host",
            path = nativeHost,
            type = "stdio",
            allowed_origins = new[]
            {
                $"chrome-extension://{AppPaths.ChromeExtensionId}/"
            }
        }, Pretty));

        File.WriteAllText(firefoxManifest, JsonSerializer.Serialize(new
        {
            name = AppPaths.NativeHostName,
            description = "SDM native messaging host",
            path = nativeHost,
            type = "stdio",
            allowed_extensions = new[] { AppPaths.FirefoxExtensionId }
        }, Pretty));

        SetHkcu($@"Software\Google\Chrome\NativeMessagingHosts\{AppPaths.NativeHostName}", chromeManifest);
        SetHkcu($@"Software\Chromium\NativeMessagingHosts\{AppPaths.NativeHostName}", chromeManifest);
        SetHkcu($@"Software\Microsoft\Edge\NativeMessagingHosts\{AppPaths.NativeHostName}", chromeManifest);
        SetHkcu($@"Software\BraveSoftware\Brave-Browser\NativeMessagingHosts\{AppPaths.NativeHostName}", chromeManifest);
        SetHkcu($@"Software\Mozilla\NativeMessagingHosts\{AppPaths.NativeHostName}", firefoxManifest);

        RegisterProtocol(appExe);
        RegisterStartup(appExe, settings.LaunchAtStartup);

        return Query();
    }

    public static BrowserIntegrationStatus Query()
    {
        var nativeHost = LocateNativeHost();
        return new BrowserIntegrationStatus
        {
            NativeHostPath = nativeHost,
            NativeHostRegistered = Registry.GetValue(
                $@"HKEY_CURRENT_USER\Software\Google\Chrome\NativeMessagingHosts\{AppPaths.NativeHostName}",
                "", null) is string,
            ProtocolRegistered = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Classes\sdm", "", null) is not null,
            ChromeExtensionPresent = File.Exists(Path.Combine(AppPaths.ChromeExtensionDir, "manifest.json")),
            FirefoxExtensionPresent = File.Exists(Path.Combine(AppPaths.FirefoxExtensionDir, "manifest.json")),
            ChromePath = AppPaths.ChromeExtensionDir,
            FirefoxPath = AppPaths.FirefoxExtensionDir
        };
    }

    public static void Uninstall()
    {
        DeleteKey($@"Software\Google\Chrome\NativeMessagingHosts\{AppPaths.NativeHostName}");
        DeleteKey($@"Software\Chromium\NativeMessagingHosts\{AppPaths.NativeHostName}");
        DeleteKey($@"Software\Microsoft\Edge\NativeMessagingHosts\{AppPaths.NativeHostName}");
        DeleteKey($@"Software\BraveSoftware\Brave-Browser\NativeMessagingHosts\{AppPaths.NativeHostName}");
        DeleteKey($@"Software\Mozilla\NativeMessagingHosts\{AppPaths.NativeHostName}");
        DeleteKey(@"Software\Classes\sdm");
        using var run = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
        run?.DeleteValue(AppPaths.ProductName, false);
    }

    public static void OpenChromeExtensionsPage()
    {
        var candidates = new[]
        {
            ("chrome.exe", "chrome://extensions/"),
            ("msedge.exe", "edge://extensions/"),
            ("brave.exe", "brave://extensions/")
        };

        foreach (var (exe, url) in candidates)
        {
            foreach (var executable in FindBrowserExecutables(exe))
            {
                if (TryStartBrowser(executable, ["--new-window", url])) return;
                if (TryStartBrowser(executable, [url])) return;
            }
        }

        if (TryStartProtocol("microsoft-edge:edge://extensions")) return;

        throw new InvalidOperationException("Chrome, Edge 또는 Brave를 찾지 못했습니다.");
    }

    public static void OpenFirefoxDebugPage()
    {
        const string url = "about:debugging#/runtime/this-firefox";
        foreach (var executable in FindBrowserExecutables("firefox.exe"))
        {
            if (TryStartBrowser(executable, ["-new-window", url])) return;
            if (TryStartBrowser(executable, [url])) return;
        }
        throw new InvalidOperationException("Firefox를 찾지 못했습니다.");
    }

    private static bool TryStartBrowser(string executable, IReadOnlyList<string> arguments)
    {
        try
        {
            AllowSetForegroundWindow(-1);
            var psi = new ProcessStartInfo
            {
                FileName = executable,
                UseShellExecute = false
            };
            foreach (var argument in arguments)
                psi.ArgumentList.Add(argument);
            Process.Start(psi);
            return true;
        }
        catch
        {
            try
            {
                AllowSetForegroundWindow(-1);
                var quoted = string.Join(" ", arguments.Select(QuoteArg));
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = executable,
                    Arguments = quoted,
                    UseShellExecute = true
                });
                return process is not null;
            }
            catch
            {
                return false;
            }
        }
    }

    private static bool TryStartProtocol(string url)
    {
        try
        {
            AllowSetForegroundWindow(-1);
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string QuoteArg(string value) =>
        value.Contains(' ') || value.Contains('#') ? $"\"{value}\"" : value;

    [DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(int dwProcessId);

    private static IEnumerable<string> FindBrowserExecutables(string executableName)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var registryKeys = new[]
        {
            $@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\App Paths\{executableName}",
            $@"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\App Paths\{executableName}",
            $@"HKEY_LOCAL_MACHINE\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\App Paths\{executableName}"
        };

        foreach (var key in registryKeys)
        {
            if (Registry.GetValue(key, "", null) is string path && IsUsableBrowserPath(path) && seen.Add(path))
                yield return path;
        }

        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var knownPaths = executableName.ToLowerInvariant() switch
        {
            "chrome.exe" => new[]
            {
                Path.Combine(local, "Google", "Chrome", "Application", executableName),
                Path.Combine(programFiles, "Google", "Chrome", "Application", executableName),
                Path.Combine(programFilesX86, "Google", "Chrome", "Application", executableName)
            },
            "msedge.exe" => new[]
            {
                Path.Combine(programFilesX86, "Microsoft", "Edge", "Application", executableName),
                Path.Combine(programFiles, "Microsoft", "Edge", "Application", executableName)
            },
            "brave.exe" => new[]
            {
                Path.Combine(local, "BraveSoftware", "Brave-Browser", "Application", executableName),
                Path.Combine(programFiles, "BraveSoftware", "Brave-Browser", "Application", executableName),
                Path.Combine(programFilesX86, "BraveSoftware", "Brave-Browser", "Application", executableName)
            },
            _ => new[]
            {
                Path.Combine(programFiles, "Mozilla Firefox", executableName),
                Path.Combine(programFilesX86, "Mozilla Firefox", executableName),
                Path.Combine(local, "Mozilla Firefox", executableName)
            }
        };

        foreach (var path in knownPaths)
            if (IsUsableBrowserPath(path) && seen.Add(path)) yield return path;
    }

    private static bool IsUsableBrowserPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return false;
        if (path.Contains(@"\WindowsApps\", StringComparison.OrdinalIgnoreCase)) return false;
        try
        {
            return new FileInfo(path).Length > 0;
        }
        catch
        {
            return false;
        }
    }

    public static void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true });
    }

    public static void RegisterStartup(string appExe, bool enabled)
    {
        using var run = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true)
                        ?? Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        if (enabled) run.SetValue(AppPaths.ProductName, $"\"{appExe}\"");
        else run.DeleteValue(AppPaths.ProductName, false);
    }

    private static void RegisterProtocol(string appExe)
    {
        using var root = Registry.CurrentUser.CreateSubKey(@"Software\Classes\sdm");
        root.SetValue("", "URL:SDM Protocol");
        root.SetValue("URL Protocol", "");
        using var cmd = root.CreateSubKey(@"shell\open\command");
        cmd.SetValue("", $"\"{appExe}\" \"%1\"");
    }

    private static void CopyExtension(string sourceRoot, string browser, string dest, AppSettings settings)
    {
        var from = Path.Combine(sourceRoot, browser);
        if (!Directory.Exists(from)) return;
        CopyTree(from, dest, skip: [".pem", ".der", ".txt"]);
        var config = new
        {
            token = settings.IpcToken,
            port = settings.HttpPort,
            nativeHost = AppPaths.NativeHostName
        };
        File.WriteAllText(Path.Combine(dest, "config.json"), JsonSerializer.Serialize(config, Pretty));
    }

    private static void CopyTree(string from, string to, string[] skip)
    {
        Directory.CreateDirectory(to);
        foreach (var dir in Directory.GetDirectories(from, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(from, dir);
            Directory.CreateDirectory(Path.Combine(to, rel));
        }
        foreach (var file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
        {
            var ext = Path.GetExtension(file);
            if (skip.Contains(ext, StringComparer.OrdinalIgnoreCase)) continue;
            var rel = Path.GetRelativePath(from, file);
            var dest = Path.Combine(to, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(file, dest, overwrite: true);
        }
    }

    private static void SetHkcu(string subkey, string value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(subkey);
        key.SetValue("", value);
    }

    private static void DeleteKey(string subkey)
    {
        try { Registry.CurrentUser.DeleteSubKeyTree(subkey, throwOnMissingSubKey: false); }
        catch { /* ignore */ }
    }

    public static string LocateAppExe()
    {
        var path = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path)) return path;
        return Path.Combine(AppContext.BaseDirectory, "SDM.exe");
    }

    public static string LocateNativeHost()
    {
        var p = Path.Combine(AppContext.BaseDirectory, "SDM.NativeHost.exe");
        return File.Exists(p) ? p : p;
    }

    public static string LocateBundledExtensions()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "extensions");
        if (Directory.Exists(dir)) return dir;
        var repo = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "extensions"));
        return Directory.Exists(repo) ? repo : dir;
    }

    private static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true };
}
