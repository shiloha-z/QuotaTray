namespace QuotaTray.Infra;

internal static class AppPaths
{
    public static readonly string DataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AgentUsageChecker");

    public static readonly string LogDir = Path.Combine(DataDir, "logs");

    public static readonly string SettingsFile = Path.Combine(DataDir, "settings.json");

    public static readonly string WebViewDataDir = Path.Combine(DataDir, "webview");

    public static readonly string CaptureFile = Path.Combine(DataDir, "captured_requests.jsonl");

    static AppPaths()
    {
        Directory.CreateDirectory(LogDir);
        Directory.CreateDirectory(WebViewDataDir);
    }
}
