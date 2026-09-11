namespace SmsNudge;

public static class NudgeLogger
{
    private static readonly object Gate = new();
    private static string _logPath = DefaultPath;

    public static string LogPath
    {
        get => _logPath;
        set => _logPath = value;
    }

    private static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SMS-autodesk-access-nudge", "nudge.log");

    public static void Info(string message) => Write("INFO", message);
    public static void Error(string message, Exception? ex = null)
        => Write("ERROR", ex is null ? message : $"{message} :: {ex}");

    private static void Write(string level, string message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_logPath)!);
            lock (Gate)
            {
                File.AppendAllText(_logPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}");
                // Keep the log small: rough truncation at ~1 MB.
                if (FileInfoSize(_logPath) > 2_000_000)
                    TruncateByHalf();
            }
        }
        catch { }
    }

    private static long FileInfoSize(string path)
    {
        try { return new FileInfo(path).Length; } catch { return 0; }
    }

    private static void TruncateByHalf() => File.WriteAllText(_logPath, string.Empty);
}
