using System.Drawing;
using System.Windows.Forms;

namespace EasyNaive.App.Presentation;

internal static class AppIcons
{
    public static Icon CreateApplicationIcon()
    {
        return CreateAssetIcon("App.ico", () =>
            Icon.ExtractAssociatedIcon(Application.ExecutablePath)
            ?? (Icon)SystemIcons.Application.Clone());
    }

    public static Icon CreateTrayStoppedIcon()
    {
        return CreateAssetIcon("TrayStopped.ico", CreateApplicationIcon);
    }

    public static Icon CreateTrayConnectedIcon()
    {
        return CreateAssetIcon("TrayConnected.ico", CreateApplicationIcon);
    }

    public static Icon CreateTrayErrorIcon()
    {
        return CreateAssetIcon("TrayError.ico", CreateApplicationIcon);
    }

    private static Icon CreateAssetIcon(string fileName, Func<Icon> fallback)
    {
        var assetIconPath = Path.Combine(AppContext.BaseDirectory, "Assets", fileName);
        if (File.Exists(assetIconPath))
        {
            return new Icon(assetIconPath);
        }

        return fallback();
    }
}
