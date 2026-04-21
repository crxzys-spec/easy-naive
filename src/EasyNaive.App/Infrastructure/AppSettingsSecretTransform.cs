using EasyNaive.Core.Models;
using EasyNaive.Platform.Windows.DataProtection;

namespace EasyNaive.App.Infrastructure;

internal sealed class AppSettingsSecretTransform : IJsonFileStoreTransform<AppSettings>
{
    private readonly IStringProtector _protector;

    public AppSettingsSecretTransform(IStringProtector protector)
    {
        _protector = protector;
    }

    public AppSettings AfterLoad(AppSettings value)
    {
        var settings = Clone(value);
        settings.ClashApiSecret = _protector.Unprotect(settings.ClashApiSecret);
        return settings;
    }

    public AppSettings BeforeSave(AppSettings value)
    {
        var settings = Clone(value);
        settings.ClashApiSecret = _protector.Protect(settings.ClashApiSecret);
        return settings;
    }

    private static AppSettings Clone(AppSettings source)
    {
        return new AppSettings
        {
            CaptureMode = source.CaptureMode,
            RouteMode = source.RouteMode,
            NodeMode = source.NodeMode,
            SelectedNodeId = source.SelectedNodeId,
            ProxyMixedPort = source.ProxyMixedPort,
            ClashApiPort = source.ClashApiPort,
            ClashApiSecret = source.ClashApiSecret,
            EnableAutoStart = source.EnableAutoStart,
            EnableMinimizeToTray = source.EnableMinimizeToTray,
            EnableTunStrictRoute = source.EnableTunStrictRoute,
            LogLevel = source.LogLevel
        };
    }
}
