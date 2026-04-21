using System.Diagnostics;

namespace EasyNaive.Platform.Windows.Shell;

public sealed class WindowsShellManager
{
    public void OpenDirectory(string path)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        };

        Process.Start(startInfo);
    }
}
