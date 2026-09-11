namespace SmsNudge;

internal sealed class TrayApplicationContext : ApplicationContext
{
    public TrayApplicationContext(AccessTrayIcon icon)
    {
        icon.ExitRequested += ExitThread;
        icon.Visible = true;
    }
}
