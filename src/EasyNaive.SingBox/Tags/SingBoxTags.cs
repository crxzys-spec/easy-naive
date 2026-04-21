using EasyNaive.Core.Models;

namespace EasyNaive.SingBox.Tags;

public static class SingBoxTags
{
    public const string ManualSelector = "manual";
    public const string AutoSelector = "auto";
    public const string ProxySelector = "proxy";
    public const string Direct = "direct";
    public const string Block = "block";

    public static string GetNodeTag(NodeProfile node) => GetNodeTag(node.Id);

    public static string GetNodeTag(string nodeId) => $"node-{nodeId}";
}
