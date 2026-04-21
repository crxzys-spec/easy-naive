using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
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

    public static Icon CreateTrayWaitingIcon()
    {
        return CreateAssetPngIcon("waiting.png", "App.ico");
    }

    public static Image CreateStatusStoppedImage()
    {
        return CreateAssetImage("stopped.png", "App.ico");
    }

    public static Image CreateStatusWaitingImage()
    {
        return CreateAssetImage("waiting.png", "App.ico");
    }

    public static Image CreateStatusConnectedImage()
    {
        return CreateAssetImage("connected.png", "TrayConnected.ico");
    }

    public static Image CreateStatusErrorImage()
    {
        return CreateAssetImage("error.png", "TrayError.ico");
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

    private static Image CreateAssetImage(string fileName, string fallbackIconName)
    {
        var assetImagePath = Path.Combine(AppContext.BaseDirectory, "Assets", fileName);
        if (File.Exists(assetImagePath))
        {
            using var source = Image.FromFile(assetImagePath);
            return new Bitmap(source);
        }

        using var icon = CreateAssetIcon(fallbackIconName, CreateApplicationIcon);
        return icon.ToBitmap();
    }

    private static Icon CreateAssetPngIcon(string fileName, string fallbackIconName)
    {
        var assetImagePath = Path.Combine(AppContext.BaseDirectory, "Assets", fileName);
        if (!File.Exists(assetImagePath))
        {
            return CreateAssetIcon(fallbackIconName, CreateApplicationIcon);
        }

        using var source = Image.FromFile(assetImagePath);
        using var bitmap = new Bitmap(64, 64);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.Transparent);
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.DrawImage(source, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
        }

        var handle = bitmap.GetHicon();
        try
        {
            using var icon = Icon.FromHandle(handle);
            return (Icon)icon.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
