using EasyNaive.Core.Enums;

namespace EasyNaive.Core.Models;

public sealed class AppSettings
{
    public CaptureMode CaptureMode { get; set; } = CaptureMode.Proxy;

    public RouteMode RouteMode { get; set; } = RouteMode.Rule;

    public NodeMode NodeMode { get; set; } = NodeMode.Manual;

    public string? SelectedNodeId { get; set; }

    public int ProxyMixedPort { get; set; } = 2080;

    public int ClashApiPort { get; set; } = 9090;

    public string ClashApiSecret { get; set; } = Guid.NewGuid().ToString("N");

    public bool EnableAutoStart { get; set; }

    public bool EnableMinimizeToTray { get; set; } = true;

    public bool EnableTunStrictRoute { get; set; } = true;

    public string LogLevel { get; set; } = "info";

    public static AppSettings CreateDefault() => new();
}
