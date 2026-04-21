namespace EasyNaive.SingBox.Config;

public sealed class SingBoxBuildContext
{
    public required string CacheFilePath { get; init; }

    public required string CnDomainRuleSetPath { get; init; }

    public required string CnIpRuleSetPath { get; init; }

    public required string ClashApiController { get; init; }
}
