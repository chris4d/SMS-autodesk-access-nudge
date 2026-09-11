using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmsNudge;

public sealed class AppConfig
{
    [JsonPropertyName("PollIntervalMinutes")]
    public int PollIntervalMinutes { get; set; } = 15;

    [JsonPropertyName("EnableAutoStart")]
    public bool EnableAutoStart { get; set; } = true;

    [JsonPropertyName("PlcFilter")]
    public List<string> PlcFilter { get; set; } = new();

    public bool MatchesPlc(string plc) => PlcFilter.Count == 0 || PlcFilter.Contains(plc, StringComparer.OrdinalIgnoreCase);

    public static AppConfig Load(string path)
    {
        try
        {
            if (!File.Exists(path)) return new AppConfig();
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
        }
        catch
        {
            return new AppConfig();
        }
    }

    public static void SaveIfMissing(string path)
    {
        try
        {
            if (File.Exists(path)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, """
                {
                  "PollIntervalMinutes": 15,
                  "EnableAutoStart": true,
                  "PlcFilter": []
                }
                """);
        }
        catch { }
    }
}
