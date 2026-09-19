using System.Text.Json;

namespace SmsNudge;

internal static class Program
{
    private const string ShutdownEventName = "SMS-autodesk-access-nudge-shutdown";
    private const string SingleInstanceMutexName = "SMS-autodesk-access-nudge-single-instance";

    [STAThread]
    private static int Main(string[] args)
    {
        var config = LoadConfig();

        if (args.Any(a => a.Equals("--once", StringComparison.OrdinalIgnoreCase)))
            return RunOnce(config);
        if (args.Any(a => a.Equals("--dump-bundles", StringComparison.OrdinalIgnoreCase)))
        {
            Console.WriteLine(new AccessStateReader().DumpRaw());
            return 0;
        }
        if (args.Any(a => a.Equals("--notify-now", StringComparison.OrdinalIgnoreCase)))
        {
            new DedupeStore(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SMS-autodesk-access-nudge", "notified-state.json")).Clear();
            var snapshot = new AccessStateReader().ReadSnapshot();
            if (snapshot.Updates.Count == 0)
            {
                ToastNotifier.ShowRunningTest();
                return 0;
            }
            ToastNotifier.ShowUpdatesAvailable(snapshot.Updates);
            return 0;
        }
        if (args.Any(a => a.Equals("--test-toast", StringComparison.OrdinalIgnoreCase)))
        {
            ApplicationConfiguration.Initialize();
            ToastNotifier.ShowRunningTest();
            return 0;
        }
        if (args.Any(a => a.Equals("--remove-autostart", StringComparison.OrdinalIgnoreCase)))
        {
            AutoStart.Disable();
            return 0;
        }
        if (args.Any(a => a.Equals("--shutdown", StringComparison.OrdinalIgnoreCase)))
        {
            if (EventWaitHandle.TryOpenExisting(ShutdownEventName, out var existing))
            {
                existing.Set();
                existing.Dispose();
            }
            return 0;
        }
        if (args.Any(a => a.Equals("--autostart", StringComparison.OrdinalIgnoreCase)))
        {
            // Register-and-exit: --autostart must NOT fall through into the tray
            // message loop. A blocking installer [Run] entry would otherwise wait
            // forever for a tray app that only exits on shutdown.
            AutoStart.Enable(Environment.ProcessPath ?? throw new InvalidOperationException("No executable path"));
            return 0;
        }

        using var shutdown = new EventWaitHandle(false, EventResetMode.AutoReset, ShutdownEventName);
        using (new Mutex(true, SingleInstanceMutexName, out var first))
        {
            if (!first) return 0;

            ApplicationConfiguration.Initialize();
            using var icon = new AccessTrayIcon(config, shutdown);
            icon.Start();
            Application.Run(new TrayApplicationContext(icon));
            return 0;
        }
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
