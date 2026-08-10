namespace QuotaTray.Infra;

internal static class Logger
{
    private static readonly object Gate = new();

    public static void Log(string message)
    {
        lock (Gate)
        {
            try
            {
                var file = Path.Combine(AppPaths.LogDir, DateTime.Now.ToString("yyyy-MM-dd") + ".log");
                File.AppendAllText(file, $"{DateTime.Now:HH:mm:ss} {message}\r\n");
            }
            catch
            {
            }
        }
    }
}
