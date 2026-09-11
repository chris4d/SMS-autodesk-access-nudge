using System.Drawing;

namespace SmsNudge;

internal sealed class AccessTrayIcon : IDisposable
{
    public event Action? ExitRequested;

    private readonly NotifyIcon _icon;
    private readonly UpdateMonitor _monitor;
    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _autostartItem;

    public AccessTrayIcon(AppConfig config)
    {
        var configDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SMS-autodesk-access-nudge");
        var dedupe = new DedupeStore(Path.Combine(configDir, "notified-state.json"));
        var reader = new AccessStateReader();
        var interval = TimeSpan.FromMinutes(Math.Max(1, config.PollIntervalMinutes));

        _statusItem = new ToolStripMenuItem("Checking…") { Enabled = false };
        _autostartItem = new ToolStripMenuItem("Start with Windows")
        {
            Checked = AutoStart.IsEnabled()
        };
        _autostartItem.Click += (_, _) =>
        {
            if (AutoStart.IsEnabled()) AutoStart.Disable();
            else AutoStart.Enable(Environment.ProcessPath ?? System.Reflection.Assembly.GetExecutingAssembly().Location);
            _autostartItem.Checked = AutoStart.IsEnabled();
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add(_statusItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Open Autodesk Access", null, (_, _) => ProductPaths.LaunchAutodeskAccess());
        menu.Items.Add("Check now", null, (_, _) => _monitor?.RunOnceManually());
        menu.Items.Add("Show test toast", null, (_, _) => ToastNotifier.ShowRunningTest());
        menu.Items.Add(_autostartItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitRequested?.Invoke());

        _icon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "Autodesk Access Nudge",
            ContextMenuStrip = menu,
            Visible = false
        };
        _icon.DoubleClick += (_, _) => ProductPaths.LaunchAutodeskAccess();
        _icon.Click += (_, _) => _monitor?.RunOnceManually();

        _monitor = new UpdateMonitor(reader, dedupe, interval, status =>
        {
            try
            {
                _icon.Text = status;
            }
            catch { }
            NudgeLogger.Info($"Status: {status}");
        }, filter: u => config.MatchesPlc(u.Plc));
    }

    public bool Visible
    {
        get => _icon.Visible;
        set => _icon.Visible = value;
    }

    public void Start() => _monitor.Start();

    public void Dispose()
    {
        _monitor.Dispose();
        _icon.Dispose();
    }
}
