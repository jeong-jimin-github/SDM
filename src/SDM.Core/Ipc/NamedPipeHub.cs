using System.IO.Pipes;
using SDM.Core.Persistence;

namespace SDM.Core.Ipc;

public sealed class NamedPipeHub : IDisposable
{
    private readonly Func<IpcMessage, Task<IpcMessage>> _handler;
    private readonly CancellationTokenSource _cts = new();
    private readonly List<Task> _clients = [];
    private readonly object _gate = new();
    private Task? _listen;

    public NamedPipeHub(Func<IpcMessage, Task<IpcMessage>> handler) => _handler = handler;

    public void Start() => _listen = Task.Run(ListenLoopAsync);

    private async Task ListenLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            var server = new NamedPipeServerStream(
                AppPaths.PipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);

            try
            {
                await server.WaitForConnectionAsync(_cts.Token).ConfigureAwait(false);
                var task = Task.Run(() => HandleClientAsync(server, _cts.Token));
                lock (_gate) _clients.Add(task);
            }
            catch (OperationCanceledException)
            {
                await server.DisposeAsync();
                break;
            }
            catch
            {
                await server.DisposeAsync();
            }
        }
    }

    private async Task HandleClientAsync(NamedPipeServerStream server, CancellationToken ct)
    {
        await using (server)
        {
            try
            {
                while (server.IsConnected && !ct.IsCancellationRequested)
                {
                    var msg = await MessageFraming.ReadAsync(server, ct).ConfigureAwait(false);
                    if (msg is null) break;
                    IpcMessage reply;
                    try { reply = await _handler(msg).ConfigureAwait(false); }
                    catch (Exception ex)
                    {
                        reply = new IpcMessage { Type = "error", Ok = false, Error = ex.Message };
                    }
                    await MessageFraming.WriteAsync(server, reply, ct).ConfigureAwait(false);
                }
            }
            catch (IOException) { }
            catch (OperationCanceledException) { }
        }
    }

    public static async Task<IpcMessage> SendAsync(IpcMessage message, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        await using var client = new NamedPipeClientStream(
            ".", AppPaths.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await client.ConnectAsync(cts.Token).ConfigureAwait(false);
        await MessageFraming.WriteAsync(client, message, cts.Token).ConfigureAwait(false);
        return await MessageFraming.ReadAsync(client, cts.Token).ConfigureAwait(false)
               ?? new IpcMessage { Type = "error", Ok = false, Error = "empty" };
    }

    public static async Task<bool> TryPingAsync()
    {
        try
        {
            var reply = await SendAsync(new IpcMessage { Type = "ping" }, TimeSpan.FromSeconds(1))
                .ConfigureAwait(false);
            return reply.Ok == true;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        try { _listen?.Wait(TimeSpan.FromSeconds(1)); } catch { /* ignore */ }
        _cts.Dispose();
    }
}
