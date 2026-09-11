using System.Timers;

namespace SmsNudge;

public sealed class UpdateMonitor : IDisposable
{
    private readonly AccessStateReader _reader;
    private readonly DedupeStore _dedupe;
    private readonly Func<AvailableUpdate, bool>? _filter;
    private readonly System.Timers.Timer _timer;
    private readonly Action<string> _onStatus;

    public UpdateMonitor(AccessStateReader reader, DedupeStore dedupe, TimeSpan interval,
        Action<string> onStatus, Func<AvailableUpdate, bool>? filter = null)
    {
        _reader = reader;
        _dedupe = dedupe;
        _onStatus = onStatus;
        _filter = filter;
        _timer = new System.Timers.Timer(interval.TotalMilliseconds);
        _timer.Elapsed += (_, _) => RunOnce(manual: false);
    }

    public void Start()
    {
        _timer.Start();
        Task.Run(() => RunOnce(manual: false));
    }

    public void RunOnceManually() => Task.Run(() => RunOnce(manual: true));

    private void RunOnce(bool manual)
    {
        try
        {
            var snapshot = _reader.ReadSnapshot();
            var updates = snapshot.Updates.Where(u => _filter?.Invoke(u) ?? true).ToList();
            var dedupeKey = string.Join("|", updates
                .Select(u => $"{u.UpgradeCode}@{u.AvailableVersion}")
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase));
            foreach (var update in updates)
                NudgeLogger.Info($"Update available: {update.DisplayName} ({update.InstalledVersion} -> {update.AvailableVersion})");
            _onStatus(updates.Count == 0
                ? $"Up to date — checked {DateTime.Now:h:mm tt}"
                : $"{updates.Count} update(s) available — checked {DateTime.Now:h:mm tt}");

            var previous = _dedupe.Load();
            var sameState = previous?.LastNotifiedKey == dedupeKey;
            NudgeLogger.Info($"Check {(manual ? "(manual)" : "(scheduled)")}: state={dedupeKey} sameAsLastNotified={sameState}");

            if (updates.Count > 0 && !sameState)
            {
                ToastNotifier.ShowUpdatesAvailable(updates);
                _dedupe.Save(new NotifiedState(dedupeKey, DateTime.UtcNow.ToString("o")));
                NudgeLogger.Info($"Notified for state {dedupeKey}");
            }
        }
        catch (Exception ex)
        {
            NudgeLogger.Error("Check failed", ex);
            _onStatus($"Check failed: {ex.Message}");
        }
    }

    public void Dispose() => _timer.Dispose();
}
