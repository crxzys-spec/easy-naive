using System.Windows.Forms;
using EasyNaive.Platform.Windows.Instance;

namespace EasyNaive.App;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var singleInstance = WindowsSingleInstanceGuard.CreateDefault();
        if (!singleInstance.IsPrimaryInstance)
        {
            return;
        }

        ApplicationConfiguration.Initialize();

        using var appContext = AppBootstrapper.Create();
        Application.Run(appContext);
        GC.KeepAlive(singleInstance);
    }
}
