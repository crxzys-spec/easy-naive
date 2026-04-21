using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using EasyNaive.Core.Models;

namespace EasyNaive.App.Subscriptions;

internal sealed class SubscriptionImportService : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public SubscriptionImportService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _ownsHttpClient = httpClient is null;
    }

    public async Task<IReadOnlyList<NodeProfile>> DownloadNodesAsync(SubscriptionProfile subscription, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(subscription.Url, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException("Subscription URL is invalid.");
        }

        using var response = await _httpClient.GetAsync(uri, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParseNodes(subscription, content);
    }

    public IReadOnlyList<NodeProfile> ParseNodes(SubscriptionProfile subscription, string content)
    {
        var normalizedContent = content.Trim();
        if (string.IsNullOrWhiteSpace(normalizedContent))
        {
            throw new InvalidOperationException("Subscription content is empty.");
        }

        var parsedNodes = TryParsePayload(subscription, normalizedContent);
        if (parsedNodes.Count > 0)
        {
            return parsedNodes;
        }

        var decodedContent = TryDecodeBase64(normalizedContent);
        if (!string.IsNullOrWhiteSpace(decodedContent))
        {
            parsedNodes = TryParsePayload(subscription, decodedContent.Trim());
            if (parsedNodes.Count > 0)
            {
                return parsedNodes;
            }
        }

        throw new InvalidOperationException("Subscription format is not supported or contains no valid naive nodes.");
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    private static List<NodeProfile> TryParsePayload(SubscriptionProfile subscription, string payload)
    {
        var nodes = new List<NodeProfile>();

        if (payload.StartsWith('{') || payload.StartsWith('['))
        {
            nodes.AddRange(ParseJsonNodes(subscription, payload));
        }
        else
        {
            nodes.AddRange(ParseTextNodes(subscription, payload));
        }

        NormalizeNodes(nodes, subscription);
        return nodes;
    }

    private static IEnumerable<NodeProfile> ParseJsonNodes(SubscriptionProfile subscription, string payload)
    {
        JsonNode? root;

        try
        {
            root = JsonNode.Parse(payload);
        }
        catch (JsonException)
        {
            return Array.Empty<NodeProfile>();
        }

        if (root is null)
        {
            return Array.Empty<NodeProfile>();
        }

        var objects = ExtractJsonObjects(root).ToArray();
        var nodes = new List<NodeProfile>(objects.Length);

        foreach (var nodeObject in objects)
        {
            var parsed = TryParseJsonNode(nodeObject, subscription);
            if (parsed is not null)
            {
                nodes.Add(parsed);
            }
        }

        return nodes;
    }

    private static IEnumerable<JsonObject> ExtractJsonObjects(JsonNode root)
    {
        return root switch
        {
            JsonArray array => array.OfType<JsonObject>(),
            JsonObject obj when obj["nodes"] is JsonArray nodes => nodes.OfType<JsonObject>(),
            JsonObject obj when obj["outbounds"] is JsonArray outbounds => outbounds.OfType<JsonObject>(),
            JsonObject obj => new[] { obj },
            _ => Array.Empty<JsonObject>()
        };
    }

    private static NodeProfile? TryParseJsonNode(JsonObject nodeObject, SubscriptionProfile subscription)
    {
        var type = GetString(nodeObject, "type");
        if (!string.IsNullOrWhiteSpace(type) && !string.Equals(type, "naive", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var server = GetString(nodeObject, "server") ?? GetString(nodeObject, "host");
        var port = GetInt(nodeObject, "server_port") ?? GetInt(nodeObject, "port") ?? 443;

        if (string.IsNullOrWhiteSpace(server))
        {
            return null;
        }

        var tlsServerName = nodeObject["tls"]?["server_name"]?.GetValue<string?>()
                            ?? GetString(nodeObject, "tls_server_name")
                            ?? GetString(nodeObject, "sni")
                            ?? server;

        return new NodeProfile
        {
            Id = Guid.NewGuid().ToString("N"),
            SubscriptionId = subscription.Id,
            Name = GetString(nodeObject, "name")
                ?? GetString(nodeObject, "tag")
                ?? GetString(nodeObject, "remark")
                ?? $"{server}:{port}",
            Group = GetString(nodeObject, "group") ?? subscription.Name,
            Server = server,
            ServerPort = port,
            Username = GetString(nodeObject, "username") ?? GetString(nodeObject, "user") ?? string.Empty,
            Password = GetString(nodeObject, "password") ?? GetString(nodeObject, "pass") ?? string.Empty,
            TlsServerName = tlsServerName,
            UseQuic = GetBool(nodeObject, "quic"),
            UseUdpOverTcp = GetUdpOverTcp(nodeObject),
            Enabled = GetOptionalBool(nodeObject, "enabled") ?? true,
            Remark = GetString(nodeObject, "remark") ?? string.Empty
        };
    }

    private static IEnumerable<NodeProfile> ParseTextNodes(SubscriptionProfile subscription, string payload)
    {
        var lines = payload
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !line.StartsWith('#'))
            .ToArray();

        var nodes = new List<NodeProfile>(lines.Length);

        foreach (var line in lines)
        {
            var parsed = TryParseNaiveUri(subscription, line);
            if (parsed is not null)
            {
                nodes.Add(parsed);
            }
        }

        return nodes;
    }

    private static NodeProfile? TryParseNaiveUri(SubscriptionProfile subscription, string rawLine)
    {
        if (!Uri.TryCreate(rawLine, UriKind.Absolute, out var uri))
        {
            return null;
        }

        if (!uri.Scheme.StartsWith("naive", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var rawUserInfo = uri.UserInfo ?? string.Empty;
        var separatorIndex = rawUserInfo.IndexOf(':');
        var username = separatorIndex >= 0 ? rawUserInfo[..separatorIndex] : rawUserInfo;
        var password = separatorIndex >= 0 ? rawUserInfo[(separatorIndex + 1)..] : string.Empty;
        var query = ParseQuery(uri.Query);
        var server = uri.Host;
        var port = uri.IsDefaultPort ? 443 : uri.Port;
        var fragment = uri.Fragment.TrimStart('#');
        var name = Uri.UnescapeDataString(string.IsNullOrWhiteSpace(fragment) ? $"{server}:{port}" : fragment);

        return new NodeProfile
        {
            Id = Guid.NewGuid().ToString("N"),
            SubscriptionId = subscription.Id,
            Name = name,
            Group = query.TryGetValue("group", out var group) && !string.IsNullOrWhiteSpace(group)
                ? group
                : subscription.Name,
            Server = server,
            ServerPort = port,
            Username = Uri.UnescapeDataString(username),
            Password = Uri.UnescapeDataString(password),
            TlsServerName = FirstNonEmpty(query, "sni", "server_name", "peer") ?? server,
            UseQuic = ParseBoolean(FirstNonEmpty(query, "quic")),
            UseUdpOverTcp = ParseBoolean(FirstNonEmpty(query, "udp_over_tcp", "uot")),
            Enabled = true,
            Remark = string.Empty
        };
    }

    private static void NormalizeNodes(List<NodeProfile> nodes, SubscriptionProfile subscription)
    {
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in nodes)
        {
            node.Name = string.IsNullOrWhiteSpace(node.Name) ? $"{node.Server}:{node.ServerPort}" : node.Name.Trim();
            node.Group = string.IsNullOrWhiteSpace(node.Group) ? subscription.Name : node.Group.Trim();
            node.SubscriptionId = subscription.Id;
            node.TlsServerName = string.IsNullOrWhiteSpace(node.TlsServerName) ? node.Server : node.TlsServerName.Trim();
            node.Server = node.Server.Trim();
            node.Username = node.Username.Trim();
            node.Password = node.Password.Trim();
            node.Remark = node.Remark.Trim();

            if (!seenNames.Add(node.Name))
            {
                node.Name = $"{node.Name} ({node.Server}:{node.ServerPort})";
                seenNames.Add(node.Name);
            }
        }
    }

    private static string? TryDecodeBase64(string content)
    {
        var compact = new string(content.Where(character => !char.IsWhiteSpace(character)).ToArray());
        if (compact.Length == 0 || compact.Length % 4 != 0)
        {
            return null;
        }

        if (compact.Any(character =>
                !char.IsLetterOrDigit(character) &&
                character != '+' &&
                character != '/' &&
                character != '='))
        {
            return null;
        }

        try
        {
            var bytes = Convert.FromBase64String(compact);
            return Encoding.UTF8.GetString(bytes);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var trimmed = query.TrimStart('?');
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return values;
        }

        foreach (var pair in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separatorIndex = pair.IndexOf('=');
            if (separatorIndex < 0)
            {
                values[Uri.UnescapeDataString(pair)] = string.Empty;
                continue;
            }

            var key = Uri.UnescapeDataString(pair[..separatorIndex]);
            var value = Uri.UnescapeDataString(pair[(separatorIndex + 1)..]);
            values[key] = value;
        }

        return values;
    }

    private static string? FirstNonEmpty(IReadOnlyDictionary<string, string> values, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static bool ParseBoolean(string? value)
    {
        return value is not null &&
               (string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "on", StringComparison.OrdinalIgnoreCase));
    }

    private static string? GetString(JsonObject obj, string propertyName)
    {
        return obj[propertyName]?.GetValue<string?>();
    }

    private static int? GetInt(JsonObject obj, string propertyName)
    {
        return obj[propertyName]?.GetValue<int?>();
    }

    private static bool GetBool(JsonObject obj, string propertyName)
    {
        return GetOptionalBool(obj, propertyName) ?? false;
    }

    private static bool? GetOptionalBool(JsonObject obj, string propertyName)
    {
        return obj[propertyName] switch
        {
            JsonValue value when value.TryGetValue<bool>(out var boolValue) => boolValue,
            JsonValue value when value.TryGetValue<int>(out var intValue) => intValue != 0,
            JsonValue value when value.TryGetValue<string>(out var stringValue) => ParseBoolean(stringValue),
            _ => null
        };
    }

    private static bool GetUdpOverTcp(JsonObject obj)
    {
        if (obj["udp_over_tcp"] is JsonObject udpObject)
        {
            return GetOptionalBool(udpObject, "enabled") ?? true;
        }

        return GetBool(obj, "udp_over_tcp");
    }
}
