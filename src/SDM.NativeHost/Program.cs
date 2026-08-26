using System.Diagnostics;
using System.IO.Pipes;
using SDM.Core.Ipc;
using SDM.Core.Persistence;

AppPaths.EnsureCreated();
var logPath = Path.Combine(AppPaths.Root, "native-host.log");

try
{
    await using var stdin = Console.OpenStandardInput();
    await using var stdout = Console.OpenStandardOutput();

    await EnsureAppRunningAsync().ConfigureAwait(false);

    await using var pipe = new NamedPipeClientStream(
        ".", AppPaths.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
    using var connectCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    await pipe.ConnectAsync(connectCts.Token).ConfigureAwait(false);

    while (true)
    {
        var incoming = await MessageFraming.ReadAsync(stdin, CancellationToken.None).ConfigureAwait(false);
        if (incoming is null) break;
        await MessageFraming.WriteAsync(pipe, incoming, CancellationToken.None).ConfigureAwait(false);
        var reply = await MessageFraming.ReadAsync(pipe, CancellationToken.None).ConfigureAwait(false)
                    ?? new IpcMessage { Type = "error", Ok = false, Error = "no-reply" };
        await MessageFraming.WriteAsync(stdout, reply, CancellationToken.None).ConfigureAwait(false);
    }
}
catch (Exception ex)
{
    try
    {
        await File.AppendAllTextAsync(logPath, $"{DateTime.Now:o} {ex}\n").ConfigureAwait(false);
    }
    catch
    {
        // ignore
    }
}

static async Task EnsureAppRunningAsync()
{
    if (await NamedPipeHub.TryPingAsync().ConfigureAwait(false)) return;

    var dir = AppContext.BaseDirectory;
    var candidates = new[]
    {
        Path.Combine(dir, "SDM.exe"),
        Path.Combine(dir, "SDM.App.exe")
    };
    var exe = candidates.FirstOrDefault(File.Exists);
    if (exe is null) throw new FileNotFoundException("SDM.exe 를 찾을 수 없습니다.", dir);

    Process.Start(new ProcessStartInfo(exe)
    {
        UseShellExecute = true,
        WorkingDirectory = dir
    });

    for (var i = 0; i < 50; i++)
    {
        await Task.Delay(200).ConfigureAwait(false);
        if (await NamedPipeHub.TryPingAsync().ConfigureAwait(false)) return;
    }

    throw new TimeoutException("SDM 앱이 시작되지 않았습니다.");
}
