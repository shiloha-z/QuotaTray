using QuotaTray.Tray;
using System.Windows.Forms;

namespace QuotaTray;

internal static class Program
{
    private const string MutexName = "Local\\QuotaTray.SingleInstance";

    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApp());
    }
}
