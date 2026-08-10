using System.Windows.Forms;

namespace QuotaTray.Infra;

internal static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "QuotaTray";
    private const string LegacyName = "AgentUsageChecker";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(AppName) != null;
        }
        catch
        {
            return false;
        }
    }

    public static void Set(bool enabled)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RunKey);
            if (enabled)
            {
                key.SetValue(AppName, "\"" + Application.ExecutablePath + "\"");
            }
            else
            {
                key.DeleteValue(AppName, false);
            }
        }
        catch (Exception ex)
        {
            Logger.Log("autostart error: " + ex.Message);
        }
    }

    /// <summary>清除旧版本遗留的自启注册项名（AgentUsageChecker）。</summary>
    public static void CleanupLegacy()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RunKey);
            key.DeleteValue(LegacyName, false);
        }
        catch
        {
        }
    }
}
