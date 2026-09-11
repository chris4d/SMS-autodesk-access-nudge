using System.Text.Json;

namespace SmsNudge;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var config = LoadConfig();

        if (args.Any(a => a.Equals("--once", StringComparison.OrdinalIgnoreCase)))
            return RunOnce(config);
        if (args.Any(a => a.Equals("--test-toast", StringComparison.OrdinalIgnoreCase)))
        {
            ApplicationConfiguration.Initialize();
            ToastNotifier.ShowRunningTest();
            return 0;
        }

        ApplicationConfiguration.Initialize();
        using var icon = new AccessTrayIcon(config);
        icon.Start();
        Application.Run(new TrayApplicationContext(icon));
        return 0;
    }

    private static AppConfig LoadConfig()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SMS-autodesk-access-nudge", "config.json");
        AppConfig.SaveIfMissing(path);
        return AppConfig.Load(path);
    }

    private static int RunOnce(AppConfig config)
    {
        var reader = new AccessStateReader();
        try
        {
            var snapshot = reader.ReadSnapshot();
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                @checked = snapshot.CapturedUtc,
                updatesAvailable = snapshot.Updates.Count,
                updates = snapshot.Updates.Select(u => new
                {
                    product = u.Name,
                    plc = u.Plc,
                    release = u.Release,
                    installed = u.InstalledVersion,
                    available = u.AvailableVersion
                })
            }, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to read Autodesk Access state: {ex.Message}");
            return 2;
        }
    }
}
