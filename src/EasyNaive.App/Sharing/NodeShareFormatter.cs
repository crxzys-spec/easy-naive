using System.Text;
using EasyNaive.Core.Models;

namespace EasyNaive.App.Sharing;

internal static class NodeShareFormatter
{
    public static string FormatNodes(IEnumerable<NodeProfile> nodes)
    {
        return string.Join(Environment.NewLine, nodes.Select(FormatNode));
    }

    public static string FormatNode(NodeProfile node)
    {
        if (string.IsNullOrWhiteSpace(node.Server))
        {
            throw new InvalidOperationException($"Node \"{node.Name}\" does not have a server.");
        }

        var builder = new StringBuilder();
        builder.Append("naive://");
        builder.Append(Uri.EscapeDataString(node.Username));
        builder.Append(':');
        builder.Append(Uri.EscapeDataString(node.Password));
        builder.Append('@');
        builder.Append(FormatHost(node.Server.Trim()));
        builder.Append(':');
        builder.Append(node.ServerPort);

        var query = BuildQuery(node);
        if (query.Count > 0)
        {
            builder.Append('?');
            builder.Append(string.Join("&", query));
        }

        if (!string.IsNullOrWhiteSpace(node.Name))
        {
            builder.Append('#');
            builder.Append(Uri.EscapeDataString(node.Name.Trim()));
        }

        return builder.ToString();
    }

    private static List<string> BuildQuery(NodeProfile node)
    {
        var query = new List<string>();

        AddQueryValue(query, "group", node.Group);

        if (!string.IsNullOrWhiteSpace(node.TlsServerName) &&
            !string.Equals(node.TlsServerName.Trim(), node.Server.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            AddQueryValue(query, "sni", node.TlsServerName);
        }

        if (node.UseQuic)
        {
            query.Add("quic=1");
        }

        if (node.UseUdpOverTcp)
        {
            query.Add("udp_over_tcp=1");
        }

        return query;
    }

    private static void AddQueryValue(List<string> query, string key, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        query.Add($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value.Trim())}");
    }

    private static string FormatHost(string server)
    {
        return server.Contains(':', StringComparison.Ordinal) &&
               !server.StartsWith("[", StringComparison.Ordinal) &&
               !server.EndsWith("]", StringComparison.Ordinal)
            ? $"[{server}]"
            : server;
    }
}
