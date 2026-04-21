namespace EasyNaive.Core.Models;

public sealed class SystemProxySnapshot
{
    public bool ProxyEnabled { get; set; }

    public bool ProxyServerExists { get; set; }

    public string ProxyServer { get; set; } = string.Empty;

    public int ManagedPort { get; set; }

    public DateTimeOffset CapturedAt { get; set; }
}
