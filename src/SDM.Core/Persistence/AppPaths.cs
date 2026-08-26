namespace SDM.Core.Persistence;

public static class AppPaths
{
    public const string PipeName = "SDM.Ipc.v1";
    public const string MutexName = "SDM.SingleInstance.v1";
    public const string ProtocolName = "sdm";
    public const string NativeHostName = "com.sdm.host";
    public const string ProductName = "SDM";
    public const string Version = "1.0.0";
    public const string FirefoxExtensionId = "sdm@sdm.app";
    public const string ChromeExtensionId = "fhbhdkhigkfjlbphkjllhpoaolnlnlhc";

    public static string Root =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ProductName);

    public static string JobsFile => Path.Combine(Root, "jobs.json");
    public static string SettingsFile => Path.Combine(Root, "settings.json");
    public static string LogFile => Path.Combine(Root, "sdm.log");
    public static string NativeManifestDir => Path.Combine(Root, "native-messaging");
    public static string ExtensionsDir => Path.Combine(Root, "extensions");
    public static string ChromeExtensionDir => Path.Combine(ExtensionsDir, "chrome");
    public static string FirefoxExtensionDir => Path.Combine(ExtensionsDir, "firefox");

    public static void EnsureCreated() => Directory.CreateDirectory(Root);
}
