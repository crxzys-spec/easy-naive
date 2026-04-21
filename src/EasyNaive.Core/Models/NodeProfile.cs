namespace EasyNaive.Core.Models;

public sealed class NodeProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string SubscriptionId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Group { get; set; } = "Default";

    public string Server { get; set; } = string.Empty;

    public int ServerPort { get; set; } = 443;

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string TlsServerName { get; set; } = string.Empty;

    public bool UseQuic { get; set; }

    public bool UseUdpOverTcp { get; set; }

    public bool Enabled { get; set; } = true;

    public int SortOrder { get; set; }

    public string Remark { get; set; } = string.Empty;
}
