using QuotaTray.Infra;
using QuotaTray.Tray;
using System.Windows.Forms;

namespace QuotaTray;

internal static class Program
{
    private const string MutexName = "Local\\QuotaTray.SingleInstance";

    [STAThread]
    private static void Main()
    {
        // 全局异常处理：防止启动/运行期异常导致静默退出
        Application.ThreadException += (_, e) => HandleFatal(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            HandleFatal(e.ExceptionObject as Exception);

        using var mutex = new Mutex(true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show("QuotaTray 已经在运行了。", "QuotaTray",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new TrayApp());
        }
        catch (Exception ex)
        {
            HandleFatal(ex);
            throw;
        }
    }

    private static void HandleFatal(Exception? ex)
    {
        try
        {
            Logger.Log("FATAL: " + ex);
        }
        catch
        {
        }
        MessageBox.Show(ex?.ToString() ?? "未知错误", "QuotaTray 启动失败",
            MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
