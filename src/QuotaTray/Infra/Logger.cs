namespace QuotaTray.Infra;

internal static class Logger
{
    private static readonly object Gate = new();
    private static DateTime _lastCleanup = DateTime.MinValue;

    public static void Log(string message)
    {
        lock (Gate)
        {
            try
            {
                CleanupOldLogsIfNeeded();
                var file = Path.Combine(AppPaths.LogDir, DateTime.Now.ToString("yyyy-MM-dd") + ".log");
                File.AppendAllText(file, $"{DateTime.Now:HH:mm:ss} {Sanitize(message)}\r\n");
            }
            catch
            {
            }
        }
    }

    private static void CleanupOldLogsIfNeeded()
    {
        var now = DateTime.Now;
        if ((now - _lastCleanup).TotalHours < 6)
        {
            return;
        }

        _lastCleanup = now;
        try
        {
            var cutoff = now.AddDays(-7);
            foreach (var file in Directory.EnumerateFiles(AppPaths.LogDir, "*.log"))
            {
                if (DateTime.TryParse(Path.GetFileNameWithoutExtension(file), out var date) && date < cutoff)
                {
                    try { File.Delete(file); } catch { }
                }
            }
        }
        catch
        {
        }
    }

    private static string Sanitize(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return message;
        }

        var user = Environment.UserName;
        if (!string.IsNullOrEmpty(user) && message.Contains(user, StringComparison.Ordinal))
        {
            message = message.Replace(user, "~", StringComparison.Ordinal);
        }

        return message;
    }
}
