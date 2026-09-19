using System.Diagnostics;
using System.Drawing;

namespace SmsNudge;

internal sealed class AccessTrayIcon : IDisposable
{
    public event Action? ExitRequested;

    private readonly NotifyIcon _icon;
    private readonly UpdateMonitor _monitor;
    private readonly DedupeStore _dedupe;
    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _autostartItem;
    private readonly EventWaitHandle _shutdownSignal;

    public AccessTrayIcon(AppConfig config, EventWaitHandle shutdownSignal)
    {
        _shutdownSignal = shutdownSignal;
        var configDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SMS-autodesk-access-nudge");
        _dedupe = new DedupeStore(Path.Combine(configDir, "notified-state.json"));
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
            else if (Environment.ProcessPath is { } exePath) AutoStart.Enable(exePath);
            _autostartItem.Checked = AutoStart.IsEnabled();
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add(_statusItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Open Autodesk Access", null, (_, _) => ProductPaths.LaunchAutodeskAccess());
        menu.Items.Add("Open notification settings", null, (_, _) =>
            Process.Start(new ProcessStartInfo("ms-settings:notifications") { UseShellExecute = true }));
        menu.Items.Add(_autostartItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitRequested?.Invoke());

        _icon = new NotifyIcon
        {
            Icon = LoadAppIcon(),
            Text = "SMS Autodesk Access Nudge",
            ContextMenuStrip = menu,
            Visible = false
        };
        _icon.DoubleClick += (_, _) => ProductPaths.LaunchAutodeskAccess();

        _monitor = new UpdateMonitor(reader, _dedupe, interval, status =>
        {
            try
            {
                _icon.Text = status;
            }
            catch { }
            NudgeLogger.Info($"Status: {status}");
        }, filter: u => config.MatchesPlc(u.Plc));
    }

    private Icon LoadAppIcon()
    {
        try
        {
            var icoPath = Path.Combine(AppContext.BaseDirectory, "assets", "app-icon.ico");
            if (File.Exists(icoPath))
                return new Icon(icoPath);
            if (Environment.ProcessPath is { } exe)
                return Icon.ExtractAssociatedIcon(exe) ?? SystemIcons.Application;
        }
        catch (Exception ex)
        {
            NudgeLogger.Error("Falling back to system icon", ex);
        }
        return SystemIcons.Application;
    }

    public bool Visible
    {
        get => _icon.Visible;
        set => _icon.Visible = value;
    }

    public void Start()
    {
        _monitor.Start();
        Task.Run(WaitForShutdownSignal);
    }

    private void WaitForShutdownSignal()
    {
        try
        {
            _shutdownSignal.WaitOne();
            ExitRequested?.Invoke();
        }
        catch { }
    }

    public void Dispose()
    {
        _monitor.Dispose();
        _icon.Dispose();
    }
}

