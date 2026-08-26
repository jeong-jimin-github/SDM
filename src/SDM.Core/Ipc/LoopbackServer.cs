using System.Net;
using System.Text;
using SDM.Core.Models;

namespace SDM.Core.Ipc;

public sealed class LoopbackServer : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly Func<IpcMessage, Task<IpcMessage>> _handler;
    private readonly AppSettings _settings;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public LoopbackServer(AppSettings settings, Func<IpcMessage, Task<IpcMessage>> handler)
    {
        _settings = settings;
        _handler = handler;
    }

    public int Port => _settings.HttpPort;
    public bool IsListening => _listener.IsListening;

    public void Start()
    {
        var prefix = $"http://127.0.0.1:{_settings.HttpPort}/";
        _listener.Prefixes.Clear();
        _listener.Prefixes.Add(prefix);
        _listener.Start();
        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => ListenAsync(_cts.Token));
    }

    private async Task ListenAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            HttpListenerContext? ctx = null;
            try
            {
                ctx = await _listener.GetContextAsync().WaitAsync(ct).ConfigureAwait(false);
                _ = Task.Run(() => HandleAsync(ctx), ct);
            }
            catch (OperationCanceledException) { break; }
            catch
            {
                // keep serving
            }
        }
    }

    private async Task HandleAsync(HttpListenerContext ctx)
    {
        var req = ctx.Request;
        var res = ctx.Response;
        try
        {
            res.Headers["Access-Control-Allow-Origin"] = req.Headers["Origin"] ?? "*";
            res.Headers["Access-Control-Allow-Headers"] = "Content-Type, X-SDM-Token";
            res.Headers["Access-Control-Allow-Methods"] = "GET, POST, OPTIONS";

            if (req.HttpMethod == "OPTIONS")
            {
                res.StatusCode = 204;
                return;
            }

            var host = req.UserHostName ?? "";
            if (!host.StartsWith("127.0.0.1") && !host.StartsWith("localhost"))
            {
                res.StatusCode = 403;
                return;
            }

            if (req.Url?.AbsolutePath.Equals("/v1/ping", StringComparison.OrdinalIgnoreCase) == true
                && req.HttpMethod == "GET")
            {
                await WriteJsonAsync(res, new IpcMessage
                {
                    Type = "pong",
                    Ok = true,
                    Version = Persistence.AppPaths.Version
                }).ConfigureAwait(false);
                return;
            }

            if (req.HttpMethod != "POST")
            {
                res.StatusCode = 405;
                return;
            }

            using var reader = new StreamReader(req.InputStream, req.ContentEncoding);
            var body = await reader.ReadToEndAsync().ConfigureAwait(false);
            var message = string.IsNullOrWhiteSpace(body)
                ? new IpcMessage { Type = "ping" }
                : MessageFraming.FromJson(body);

            var token = req.Headers["X-SDM-Token"] ?? message.Token;
            var origin = req.Headers["Origin"] ?? "";
            var trustedOrigin = origin.StartsWith("chrome-extension://", StringComparison.OrdinalIgnoreCase)
                                || origin.StartsWith("moz-extension://", StringComparison.OrdinalIgnoreCase)
                                || string.IsNullOrEmpty(origin);
            if (!string.IsNullOrEmpty(_settings.IpcToken) &&
                !string.IsNullOrEmpty(token) &&
                !string.Equals(token, _settings.IpcToken, StringComparison.Ordinal) &&
                !trustedOrigin &&
                message.Type is not "ping")
            {
                res.StatusCode = 401;
                await WriteJsonAsync(res, new IpcMessage { Type = "error", Ok = false, Error = "token" })
                    .ConfigureAwait(false);
                return;
            }

            var reply = await _handler(message).ConfigureAwait(false);
            await WriteJsonAsync(res, reply).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            try
            {
                res.StatusCode = 500;
                await WriteJsonAsync(res, new IpcMessage { Type = "error", Ok = false, Error = ex.Message })
                    .ConfigureAwait(false);
            }
            catch { /* ignore */ }
        }
        finally
        {
            try { res.Close(); } catch { /* ignore */ }
        }
    }

    private static async Task WriteJsonAsync(HttpListenerResponse res, IpcMessage message)
    {
        var json = MessageFraming.ToJson(message);
        var bytes = Encoding.UTF8.GetBytes(json);
        res.ContentType = "application/json; charset=utf-8";
        res.ContentLength64 = bytes.Length;
        res.StatusCode = 200;
        await res.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
    }

    public void Dispose()
    {
        try { _cts?.Cancel(); } catch { /* ignore */ }
        try { _listener.Stop(); } catch { /* ignore */ }
        _listener.Close();
        _cts?.Dispose();
    }
}
