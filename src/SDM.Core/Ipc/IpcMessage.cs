using System.Text.Json.Serialization;

namespace SDM.Core.Ipc;

public sealed class MediaHit
{
    public string Url { get; set; } = "";
    public string? Mime { get; set; }
    public long? Size { get; set; }
    public string? PageUrl { get; set; }
    public string? PageTitle { get; set; }
    public string? TabTitle { get; set; }
}

public sealed class IpcMessage
{
    public string Type { get; set; } = "";
    public string? Url { get; set; }
    [JsonPropertyName("filename")]
    public string? Filename { get; set; }
    public string? Referrer { get; set; }
    public string? Cookies { get; set; }
    public string? UserAgent { get; set; }
    public string? Mime { get; set; }
    public string? PageUrl { get; set; }
    public string? PageTitle { get; set; }
    public string? SavePath { get; set; }
    public string? Token { get; set; }
    public string? Error { get; set; }
    public string? Version { get; set; }
    public string? Browser { get; set; }
    public long? FileSize { get; set; }
    public int? Connections { get; set; }
    public bool? Start { get; set; }
    public bool? Ok { get; set; }
    public bool? Confirm { get; set; }
    public Guid? JobId { get; set; }
    public Dictionary<string, string>? Headers { get; set; }
    public List<MediaHit>? Media { get; set; }
}
