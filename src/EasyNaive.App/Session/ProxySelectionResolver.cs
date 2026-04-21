using EasyNaive.Core.Models;
using EasyNaive.SingBox.Tags;

namespace EasyNaive.App.Session;

internal static class ProxySelectionResolver
{
    public static string ResolveCurrentRealNodeId(
        string? proxySelection,
        string? manualSelection,
        string? autoSelection,
        IEnumerable<NodeProfile> nodes)
    {
        var effectiveTag = proxySelection switch
        {
            SingBoxTags.ManualSelector => manualSelection,
            SingBoxTags.AutoSelector => autoSelection,
            _ => proxySelection
        };

        if (string.IsNullOrWhiteSpace(effectiveTag))
        {
            return string.Empty;
        }

        var matchedNode = nodes.FirstOrDefault(node =>
            string.Equals(SingBoxTags.GetNodeTag(node), effectiveTag, StringComparison.Ordinal));

        return matchedNode?.Id ?? string.Empty;
    }
}
