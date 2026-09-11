using Microsoft.Toolkit.Uwp.Notifications;

namespace SmsNudge;

public static class ToastNotifier
{
    public static void ShowUpdatesAvailable(IReadOnlyList<AvailableUpdate> updates)
    {
        var first = updates[0];
        var body = updates.Count == 1
            ? $"{first.Name}  v{first.InstalledVersion} → v{first.AvailableVersion}"
            : $"{first.Name} and {updates.Count - 1} more product updates";

        try
        {
            new ToastContentBuilder()
                .AddText("Autodesk updates available")
                .AddText(body)
                .Show(t => t.ExpirationTime = DateTime.Now.AddHours(12));
        }
        catch (Exception ex)
        {
            NudgeLogger.Error("Toast failed", ex);
        }
    }

    public static void ShowRunningTest()
    {
        try
        {
            new ToastContentBuilder()
                .AddText("Autodesk Access Nudge running")
                .AddText("No new product updates detected right now.")
                .Show();
        }
        catch (Exception ex)
        {
            NudgeLogger.Error("Test toast failed", ex);
        }
    }
}
