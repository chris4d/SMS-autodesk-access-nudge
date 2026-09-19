using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmsNudge;

public sealed record NotifiedState(string LastNotifiedKey, string LastNotifiedUtc)
{
    public NotifiedState() : this(string.Empty, string.Empty) { }

    [JsonPropertyName("LastDigestDate")]
    public string LastDigestDate { get; set; } = string.Empty;
}

public sealed class DedupeStore
{
    private readonly string _path;

    public DedupeStore(string path)
    {
        _path = path;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    }

    public NotifiedState? Load()
    {
        try
        {
            if (!File.Exists(_path)) return null;
            return JsonSerializer.Deserialize<NotifiedState>(File.ReadAllText(_path));
        }
        catch { return null; }
    }

    public void Save(NotifiedState state)
    {
        try
        {
            File.WriteAllText(_path, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    public void Clear()
    {
        try { if (File.Exists(_path)) File.Delete(_path); } catch { }
    }
}
