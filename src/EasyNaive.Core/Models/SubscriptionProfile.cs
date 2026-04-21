namespace EasyNaive.Core.Models;

public sealed class SubscriptionProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    public int ImportedNodeCount { get; set; }

    public DateTimeOffset? LastUpdated { get; set; }

    public string LastError { get; set; } = string.Empty;
}
