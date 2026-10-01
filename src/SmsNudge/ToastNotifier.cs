using Microsoft.Toolkit.Uwp.Notifications;

namespace SmsNudge;

public static class ToastNotifier
{
    // "Autodesk Licensing Service  v17.0.0.16519" when the installed version is
    // unknown (licensing lives outside ODIS Install.db), otherwise full range.
    private static string BuildBody(IReadOnlyList<AvailableUpdate> updates, string moreSuffix)
    {
        var first = updates[0];
        if (updates.Count > 1)
            return $"{first.Name} and {updates.Count - 1} {moreSuffix}";
        return string.IsNullOrEmpty(first.InstalledVersion)
            ? $"{first.Name}  v{first.AvailableVersion}"
            : $"{first.Name}  v{first.InstalledVersion} → v{first.AvailableVersion}";
    }
    public static void ShowUpdatesAvailable(IReadOnlyList<AvailableUpdate> updates)
    {
        var first = updates[0];
        var body = BuildBody(updates, "and");

        try
        {
            new ToastContentBuilder()
                .AddText("Autodesk updates available")
                .AddText(body)
                .SetToastScenario(ToastScenario.Reminder)
                .Show();
        }
        catch (Exception ex)
        {
            NudgeLogger.Error("Toast failed", ex);
        }
    }

    public static void ShowDigest(IReadOnlyList<AvailableUpdate> updates)
    {
        var first = updates[0];
        var body = BuildBody(updates, "other pending update(s)");

        try
        {
            new ToastContentBuilder()
                .AddText("Autodesk updates pending")
                .AddText(body)
                .SetToastScenario(ToastScenario.Reminder)
                .Show();
        }
        catch (Exception ex)
        {
            NudgeLogger.Error("Digest toast failed", ex);
        }
    }

    public static void ShowRunningTest()
    {
        try
        {
            new ToastContentBuilder()
                .AddText("SMS Autodesk Access Nudge running")
                .AddText("No new product updates detected right now.")
                .Show();
        }
        catch (Exception ex)
        {
            NudgeLogger.Error("Test toast failed", ex);
        }
    }
}

