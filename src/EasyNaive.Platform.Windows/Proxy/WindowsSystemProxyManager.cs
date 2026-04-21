using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace EasyNaive.Platform.Windows.Proxy;

public sealed class WindowsSystemProxyManager
{
    private const string InternetSettingsPath = @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";
    private const int InternetOptionSettingsChanged = 39;
    private const int InternetOptionRefresh = 37;

    public void EnableLoopbackProxy(int port)
    {
        using var key = Registry.CurrentUser.CreateSubKey(InternetSettingsPath, writable: true)
            ?? throw new InvalidOperationException("Unable to open Windows internet settings registry key.");

        key.SetValue("ProxyEnable", 1, RegistryValueKind.DWord);
        key.SetValue("ProxyServer", BuildProxyServer(port), RegistryValueKind.String);
        NotifySystem();
    }

    public void DisableManagedProxy(int port)
    {
        using var key = Registry.CurrentUser.CreateSubKey(InternetSettingsPath, writable: true)
            ?? throw new InvalidOperationException("Unable to open Windows internet settings registry key.");

        key.SetValue("ProxyEnable", 0, RegistryValueKind.DWord);
        key.DeleteValue("ProxyServer", throwOnMissingValue: false);
        NotifySystem();
    }

    public bool DisableManagedProxyIfCurrent(int port)
    {
        if (!IsManagedProxyEnabled(port))
        {
            return false;
        }

        DisableManagedProxy(port);
        return true;
    }

    public void RestoreProxyState(bool proxyEnabled, bool proxyServerExists, string proxyServer)
    {
        using var key = Registry.CurrentUser.CreateSubKey(InternetSettingsPath, writable: true)
            ?? throw new InvalidOperationException("Unable to open Windows internet settings registry key.");

        key.SetValue("ProxyEnable", proxyEnabled ? 1 : 0, RegistryValueKind.DWord);
        if (proxyServerExists)
        {
            key.SetValue("ProxyServer", proxyServer, RegistryValueKind.String);
        }
        else
        {
            key.DeleteValue("ProxyServer", throwOnMissingValue: false);
        }

        NotifySystem();
    }

    public bool IsManagedProxyEnabled(int port)
    {
        var proxyEnabled = IsProxyEnabled();
        var proxyServer = GetProxyServer();
        return proxyEnabled && IsManagedProxyValue(proxyServer, port);
    }

    public bool IsProxyEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(InternetSettingsPath, writable: false);
        return Convert.ToInt32(key?.GetValue("ProxyEnable", 0)) != 0;
    }

    public string? GetProxyServer()
    {
        using var key = Registry.CurrentUser.OpenSubKey(InternetSettingsPath, writable: false);
        return key?.GetValue("ProxyServer") as string;
    }

    public bool ProxyServerExists()
    {
        using var key = Registry.CurrentUser.OpenSubKey(InternetSettingsPath, writable: false);
        return key?.GetValueNames().Contains("ProxyServer", StringComparer.OrdinalIgnoreCase) == true;
    }

    private static bool IsManagedProxyValue(string? proxyServer, int port)
    {
        if (string.IsNullOrWhiteSpace(proxyServer))
        {
            return false;
        }

        var needle = $":{port}";
        return proxyServer
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(part =>
            {
                var normalized = part.ToLowerInvariant();
                return normalized.Contains($"127.0.0.1{needle}", StringComparison.Ordinal) ||
                       normalized.Contains($"localhost{needle}", StringComparison.Ordinal);
            });
    }

    private static string BuildProxyServer(int port) => $"http://127.0.0.1:{port}";

    private static void NotifySystem()
    {
        InternetSetOption(IntPtr.Zero, InternetOptionSettingsChanged, IntPtr.Zero, 0);
        InternetSetOption(IntPtr.Zero, InternetOptionRefresh, IntPtr.Zero, 0);
    }

    [DllImport("wininet.dll", SetLastError = true)]
    private static extern bool InternetSetOption(IntPtr hInternet, int dwOption, IntPtr lpBuffer, int dwBufferLength);
}
