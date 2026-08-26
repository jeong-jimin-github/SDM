using SDM.Core.Models;

namespace SDM.Core.Persistence;

public sealed class SettingsStore
{
    public AppSettings Current { get; private set; } = new();

    public AppSettings Load()
    {
        AppPaths.EnsureCreated();
        Current = JsonFile.LoadOrCreate(AppPaths.SettingsFile, () => new AppSettings());
        if (string.IsNullOrWhiteSpace(Current.IpcToken))
            Current.IpcToken = Guid.NewGuid().ToString("N");
        if (Current.DefaultConnections is < 1 or > 32) Current.DefaultConnections = 8;
        if (Current.MaxConcurrentDownloads is < 1 or > 16) Current.MaxConcurrentDownloads = 3;
        if (Current.HttpPort is < 1024 or > 65000) Current.HttpPort = 47832;
        Save();
        return Current;
    }

    public void Save() => JsonFile.Save(AppPaths.SettingsFile, Current);
}
