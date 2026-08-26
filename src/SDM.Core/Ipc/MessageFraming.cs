using System.Text;
using System.Text.Json;

namespace SDM.Core.Ipc;

public static class MessageFraming
{
    public const int MaxBytes = 8 * 1024 * 1024;

    public static async Task WriteAsync(Stream stream, IpcMessage message, CancellationToken ct)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(message, JsonFileOptions.Ipc);
        if (json.Length > MaxBytes)
            throw new InvalidOperationException("IPC 메시지가 너무 큽니다.");
        var header = BitConverter.GetBytes(json.Length);
        await stream.WriteAsync(header, ct).ConfigureAwait(false);
        await stream.WriteAsync(json, ct).ConfigureAwait(false);
        await stream.FlushAsync(ct).ConfigureAwait(false);
    }

    public static async Task<IpcMessage?> ReadAsync(Stream stream, CancellationToken ct)
    {
        var header = new byte[4];
        if (!await ReadExactAsync(stream, header, ct).ConfigureAwait(false))
            return null;
        var len = BitConverter.ToInt32(header, 0);
        if (len is <= 0 or > MaxBytes)
            throw new InvalidOperationException($"잘못된 IPC 길이: {len}");
        var payload = new byte[len];
        if (!await ReadExactAsync(stream, payload, ct).ConfigureAwait(false))
            return null;
        return JsonSerializer.Deserialize<IpcMessage>(payload, JsonFileOptions.Ipc)
               ?? throw new InvalidOperationException("빈 IPC 메시지");
    }

    public static string ToJson(IpcMessage message) =>
        JsonSerializer.Serialize(message, JsonFileOptions.Ipc);

    public static IpcMessage FromJson(string json) =>
        JsonSerializer.Deserialize<IpcMessage>(json, JsonFileOptions.Ipc)
        ?? throw new InvalidOperationException("빈 JSON");

    public static IpcMessage FromUtf8(ReadOnlySpan<byte> utf8) =>
        JsonSerializer.Deserialize<IpcMessage>(utf8, JsonFileOptions.Ipc)
        ?? throw new InvalidOperationException("빈 JSON");

    private static async Task<bool> ReadExactAsync(Stream stream, byte[] buffer, CancellationToken ct)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var n = await stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), ct)
                .ConfigureAwait(false);
            if (n == 0) return false;
            offset += n;
        }
        return true;
    }
}

internal static class JsonFileOptions
{
    public static readonly JsonSerializerOptions Ipc = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };
}
