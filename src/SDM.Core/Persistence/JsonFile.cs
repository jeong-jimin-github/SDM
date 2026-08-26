using System.Text.Json;
using System.Text.Json.Serialization;

namespace SDM.Core.Persistence;

public static class JsonFile
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static T LoadOrCreate<T>(string path, Func<T> factory)
    {
        try
        {
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var value = JsonSerializer.Deserialize<T>(json, Options);
                if (value is not null) return value;
            }
        }
        catch
        {
            // corrupt store — start fresh
        }

        var created = factory();
        Save(path, created);
        return created;
    }

    public static void Save<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = JsonSerializer.Serialize(value, Options);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, json);
        File.Move(tmp, path, overwrite: true);
    }
}
