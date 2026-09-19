using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmsNudge;

public enum NotifMode
{
    Immediate,
    DailyDigest
}

public sealed class NotifTiming
{
    [JsonPropertyName("Mode")]
    public string Mode { get; set; } = "Immediate";

    [JsonPropertyName("DigestTime")]
    public string DigestTime { get; set; } = "16:30";

    [JsonPropertyName("DigestDays")]
    public List<string> DigestDays { get; set; } = new() { "Mon", "Tue", "Wed", "Thu", "Fri" };

    public NotifMode ResolvedMode => Mode?.Trim().ToLowerInvariant() switch
    {
        "dailydigest" => NotifMode.DailyDigest,
        "daily" => NotifMode.DailyDigest,
        _ => NotifMode.Immediate
    };

    public TimeSpan? ResolvedTime
    {
        get
        {
            var i = DigestTime.Split(':');
            if (i.Length == 2 && int.TryParse(i[0], out var h) && int.TryParse(i[1], out var m)
                && h >= 0 && h <= 23 && m >= 0 && m <= 59)
                return new TimeSpan(h, m, 0);
            return null;
        }
    }

    private static readonly Dictionary<string, DayOfWeek> DayMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Mon"] = DayOfWeek.Monday, ["Monday"] = DayOfWeek.Monday,
        ["Tue"] = DayOfWeek.Tuesday, ["Tuesday"] = DayOfWeek.Tuesday,
        ["Wed"] = DayOfWeek.Wednesday, ["Wednesday"] = DayOfWeek.Wednesday,
        ["Thu"] = DayOfWeek.Thursday, ["Thursday"] = DayOfWeek.Thursday,
        ["Fri"] = DayOfWeek.Friday, ["Friday"] = DayOfWeek.Friday,
        ["Sat"] = DayOfWeek.Saturday, ["Saturday"] = DayOfWeek.Saturday,
        ["Sun"] = DayOfWeek.Sunday, ["Sunday"] = DayOfWeek.Sunday,
    };

    public bool IsAllowedDay(DayOfWeek day) =>
        DigestDays.Count == 0 || DigestDays.Any(d => DayMap.TryGetValue(d.Trim(), out var mapped) && mapped == day);
}

public sealed class AppConfig
{
    [JsonPropertyName("PollIntervalMinutes")]
    public int PollIntervalMinutes { get; set; } = 15;

    [JsonPropertyName("EnableAutoStart")]
    public bool EnableAutoStart { get; set; } = true;

    [JsonPropertyName("PlcFilter")]
    public List<string> PlcFilter { get; set; } = new();

    [JsonPropertyName("NotifTiming")]
    public NotifTiming Timing { get; set; } = new();

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
                  "PlcFilter": [],
                  "NotifTiming": {
                    "Mode": "Immediate",
                    "DigestTime": "16:30",
                    "DigestDays": ["Mon", "Tue", "Wed", "Thu", "Fri"]
                  }
                }
                """);
        }
        catch { }
    }
}
