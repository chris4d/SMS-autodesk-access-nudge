using System.Timers;

namespace SmsNudge;

public sealed class UpdateMonitor : IDisposable
{
    private readonly AccessStateReader _reader;
    private readonly DedupeStore _dedupe;
    private readonly Func<AvailableUpdate, bool>? _filter;
    private readonly NotifTiming _timing;
    private readonly System.Timers.Timer _pollTimer;
    private readonly System.Timers.Timer _digestTimer;
    private readonly Action<string> _onStatus;
    private readonly object _sync = new();
    private IReadOnlyList<AvailableUpdate> _lastUpdates = Array.Empty<AvailableUpdate>();
    private string? _lastStateKey;

    public UpdateMonitor(AccessStateReader reader, DedupeStore dedupe, TimeSpan interval,
        Action<string> onStatus, NotifTiming? timing = null, Func<AvailableUpdate, bool>? filter = null)
    {
        _reader = reader;
        _dedupe = dedupe;
        _onStatus = onStatus;
        _filter = filter;
        _timing = timing ?? new NotifTiming();

        _pollTimer = new System.Timers.Timer(Math.Max(60000, interval.TotalMilliseconds));
        _pollTimer.Elapsed += (_, _) => RunOnce(manual: false);

        _digestTimer = new System.Timers.Timer(60000);
        _digestTimer.Elapsed += (_, _) => EvaluateDigest();
        if (_timing.ResolvedMode == NotifMode.DailyDigest) _digestTimer.Start();
    }

    public void Start()
    {
        _pollTimer.Start();
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

            lock (_sync)
            {
                _lastUpdates = updates;
                _lastStateKey = dedupeKey;
            }
            NudgeLogger.Info($"Check {(manual ? "(manual)" : "(scheduled)")}: state={dedupeKey}");

            if (_timing.ResolvedMode == NotifMode.Immediate && updates.Count > 0)
                NotifyIfNeeded(updates, dedupeKey);
        }
        catch (Exception ex)
        {
            NudgeLogger.Error("Check failed", ex);
            _onStatus($"Check failed: {ex.Message}");
        }
    }

    private void NotifyIfNeeded(IReadOnlyList<AvailableUpdate> updates, string dedupeKey)
    {
        var previous = _dedupe.Load();
        var sameState = previous?.LastNotifiedKey == dedupeKey;
        NudgeLogger.Info($"Notify decision: sameAsLastNotified={sameState}");
        if (sameState) return;
        ToastNotifier.ShowUpdatesAvailable(updates);
        _dedupe.Save(new NotifiedState(dedupeKey, DateTime.UtcNow.ToString("o")));
        NudgeLogger.Info($"Notified for state {dedupeKey}");
    }

    private void EvaluateDigest()
    {
        try
        {
            if (_timing.ResolvedMode != NotifMode.DailyDigest) { _digestTimer.Stop(); return; }
            var due = _timing.ResolvedTime is { } then && _timing.IsAllowedDay(DateTime.Now.DayOfWeek)
                && DateTime.Now.TimeOfDay >= then
                && _dedupe.Load()?.LastDigestDate != DateTime.Today.ToString("yyyy-MM-dd");
            if (!due) return;
            Task.Run(() => { RunOnce(manual: false); SendDigest(); });
        }
        catch (Exception ex)
        {
            NudgeLogger.Error("Digest evaluation failed", ex);
        }
    }

    private void SendDigest()
    {
        IReadOnlyList<AvailableUpdate> updates;
        lock (_sync) { updates = _lastUpdates; }
        var state = _dedupe.Load();
        var today = DateTime.Today.ToString("yyyy-MM-dd");

        if (state?.LastDigestDate == today)
        {
            NudgeLogger.Info("Digest already sent today; skipping");
            return;
        }
        if (updates.Count == 0)
        {
            _dedupe.Save(state is null
                ? new NotifiedState { LastDigestDate = today }
                : new NotifiedState(state.LastNotifiedKey, state.LastNotifiedUtc) { LastDigestDate = today });
            NudgeLogger.Info("No pending updates at digest time; skipped sending (marked day as digested)");
            return;
        }

        NudgeLogger.Info($"Sending daily digest with {updates.Count} update(s)");
        ToastNotifier.ShowDigest(updates);
        var baseState = state is null
            ? new NotifiedState()
            : new NotifiedState(state.LastNotifiedKey, state.LastNotifiedUtc);
        baseState.LastDigestDate = today;
        _dedupe.Save(baseState);
    }

    public void Dispose()
    {
        _pollTimer.Dispose();
        _digestTimer.Dispose();
    }
}
