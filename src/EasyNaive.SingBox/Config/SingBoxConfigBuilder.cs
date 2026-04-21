using System.Text.Json;
using System.Text.Json.Nodes;
using System.Net;
using EasyNaive.Core.Enums;
using EasyNaive.Core.Models;
using EasyNaive.SingBox.Tags;

namespace EasyNaive.SingBox.Config;

public sealed class SingBoxConfigBuilder
{
    public string BuildJson(AppSettings settings, IReadOnlyCollection<NodeProfile> nodes, SingBoxBuildContext context)
    {
        var enabledNodes = nodes
            .Where(node => node.Enabled)
            .OrderBy(node => node.SortOrder)
            .ThenBy(node => node.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var hasProxyNodes = enabledNodes.Length > 0;

        var root = new JsonObject
        {
            ["log"] = new JsonObject
            {
                ["level"] = settings.LogLevel
            },
            ["inbounds"] = BuildInbounds(settings),
            ["outbounds"] = BuildOutbounds(settings, enabledNodes),
            ["route"] = BuildRoute(settings, hasProxyNodes, context),
            ["experimental"] = BuildExperimental(settings, context)
        };

        if (hasProxyNodes)
        {
            root["dns"] = BuildDns(settings, context);
        }

        return root.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    private static JsonArray BuildInbounds(AppSettings settings)
    {
        return settings.CaptureMode switch
        {
            CaptureMode.Proxy => new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "mixed",
                    ["tag"] = "mixed-in",
                    ["listen"] = "127.0.0.1",
                    ["listen_port"] = settings.ProxyMixedPort
                }
            },
            CaptureMode.Tun => new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "tun",
                    ["tag"] = "tun-in",
                    ["interface_name"] = "EasyNaive",
                    ["address"] = new JsonArray("172.19.0.1/30", "fdfe:dcba:9876::1/126"),
                    ["auto_route"] = true,
                    ["strict_route"] = settings.EnableTunStrictRoute,
                    ["stack"] = "system"
                }
            },
            _ => throw new ArgumentOutOfRangeException(nameof(settings.CaptureMode), settings.CaptureMode, "Unknown capture mode.")
        };
    }

    private static JsonArray BuildOutbounds(AppSettings settings, IReadOnlyList<NodeProfile> nodes)
    {
        var outbounds = new JsonArray();

        foreach (var node in nodes)
        {
            var enableUdpOverTcp = node.UseUdpOverTcp || settings.CaptureMode == CaptureMode.Tun;
            var outbound = new JsonObject
            {
                ["type"] = "naive",
                ["tag"] = GetNodeTag(node),
                ["server"] = node.Server,
                ["server_port"] = node.ServerPort,
                ["username"] = node.Username,
                ["password"] = node.Password,
                ["tls"] = new JsonObject
                {
                    ["enabled"] = true,
                    ["server_name"] = string.IsNullOrWhiteSpace(node.TlsServerName) ? node.Server : node.TlsServerName
                }
            };

            if (node.UseQuic)
            {
                outbound["quic"] = true;
            }

            if (!IPAddress.TryParse(node.Server, out _))
            {
                outbound["domain_resolver"] = "dns-direct";
            }

            if (enableUdpOverTcp)
            {
                outbound["udp_over_tcp"] = new JsonObject
                {
                    ["enabled"] = true
                };
            }

            outbounds.Add(outbound);
        }

        if (nodes.Count > 0)
        {
            var nodeTags = new JsonArray();
            foreach (var node in nodes)
            {
                nodeTags.Add(GetNodeTag(node));
            }

            var selectedNode = nodes.FirstOrDefault(node => node.Id == settings.SelectedNodeId);
            var manualDefault = selectedNode is not null ? GetNodeTag(selectedNode) : GetNodeTag(nodes[0]);

            outbounds.Add(new JsonObject
            {
                ["type"] = "selector",
                ["tag"] = SingBoxTags.ManualSelector,
                ["outbounds"] = nodeTags.DeepClone(),
                ["default"] = manualDefault
            });

            outbounds.Add(new JsonObject
            {
                ["type"] = "urltest",
                ["tag"] = SingBoxTags.AutoSelector,
                ["outbounds"] = nodeTags.DeepClone(),
                ["interval"] = "3m",
                ["tolerance"] = 50
            });

            outbounds.Add(new JsonObject
            {
                ["type"] = "selector",
                ["tag"] = SingBoxTags.ProxySelector,
                ["outbounds"] = new JsonArray(SingBoxTags.ManualSelector, SingBoxTags.AutoSelector),
                ["default"] = settings.NodeMode == NodeMode.Auto ? SingBoxTags.AutoSelector : SingBoxTags.ManualSelector
            });
        }

        outbounds.Add(new JsonObject
        {
            ["type"] = "direct",
            ["tag"] = SingBoxTags.Direct
        });

        outbounds.Add(new JsonObject
        {
            ["type"] = "block",
            ["tag"] = SingBoxTags.Block
        });

        return outbounds;
    }

    private static JsonObject BuildRoute(AppSettings settings, bool hasProxyNodes, SingBoxBuildContext context)
    {
        var hasCnDomainRuleSet = File.Exists(context.CnDomainRuleSetPath);
        var hasCnIpRuleSet = File.Exists(context.CnIpRuleSetPath);
        var rules = new JsonArray();

        if (settings.CaptureMode == CaptureMode.Tun)
        {
            rules.Add(new JsonObject
            {
                ["type"] = "logical",
                ["mode"] = "or",
                ["rules"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["protocol"] = "dns"
                    },
                    new JsonObject
                    {
                        ["port"] = 53
                    }
                },
                ["action"] = "hijack-dns"
            });
        }

        rules.Add(new JsonObject
        {
            ["clash_mode"] = "direct",
            ["action"] = "route",
            ["outbound"] = SingBoxTags.Direct
        });

        rules.Add(new JsonObject
        {
            ["clash_mode"] = "global",
            ["action"] = "route",
            ["outbound"] = hasProxyNodes ? SingBoxTags.ProxySelector : SingBoxTags.Direct
        });

        rules.Add(new JsonObject
        {
            ["ip_is_private"] = true,
            ["action"] = "route",
            ["outbound"] = SingBoxTags.Direct
        });

        if (hasProxyNodes && hasCnDomainRuleSet)
        {
            rules.Add(new JsonObject
            {
                ["rule_set"] = new JsonArray("cn-domain"),
                ["action"] = "route",
                ["outbound"] = SingBoxTags.Direct
            });
        }

        if (hasProxyNodes && hasCnIpRuleSet)
        {
            rules.Add(new JsonObject
            {
                ["rule_set"] = new JsonArray("cn-ip"),
                ["action"] = "route",
                ["outbound"] = SingBoxTags.Direct
            });
        }

        return new JsonObject
        {
            ["auto_detect_interface"] = true,
            ["default_domain_resolver"] = hasProxyNodes ? "dns-direct" : null,
            ["rule_set"] = BuildRouteRuleSets(hasProxyNodes, hasCnDomainRuleSet, hasCnIpRuleSet, context),
            ["rules"] = rules,
            ["final"] = hasProxyNodes ? SingBoxTags.ProxySelector : SingBoxTags.Direct
        };
    }

    private static JsonObject BuildDns(AppSettings settings, SingBoxBuildContext context)
    {
        var rules = new JsonArray
        {
            new JsonObject
            {
                ["clash_mode"] = "direct",
                ["action"] = "route",
                ["server"] = "dns-direct"
            },
            new JsonObject
            {
                ["clash_mode"] = "global",
                ["action"] = "route",
                ["server"] = "dns-proxy"
            }
        };

        if (File.Exists(context.CnDomainRuleSetPath))
        {
            rules.Add(new JsonObject
            {
                ["rule_set"] = new JsonArray("cn-domain"),
                ["action"] = "route",
                ["server"] = "dns-direct"
            });
        }

        var dns = new JsonObject
        {
            ["servers"] = new JsonArray
            {
                BuildDirectDnsServer(settings),
                BuildProxyDnsServer()
            },
            ["rules"] = rules,
            ["final"] = settings.RouteMode == RouteMode.Direct ? "dns-direct" : "dns-proxy",
            ["strategy"] = "ipv4_only",
            ["reverse_mapping"] = true
        };

        return dns;
    }

    private static JsonObject BuildDirectDnsServer(AppSettings _)
    {
        // Avoid relying on the platform resolver here: in Rule/Direct modes the
        // direct DNS path is also what keeps domestic sites off the proxy path.
        return new JsonObject
        {
            ["type"] = "udp",
            ["tag"] = "dns-direct",
            ["server"] = "223.5.5.5",
            ["server_port"] = 53,
            ["detour"] = SingBoxTags.Direct
        };
    }

    private static JsonObject BuildProxyDnsServer()
    {
        return new JsonObject
        {
            ["type"] = "https",
            ["tag"] = "dns-proxy",
            ["server"] = "1.1.1.1",
            ["detour"] = SingBoxTags.ProxySelector
        };
    }

    private static JsonObject BuildExperimental(AppSettings settings, SingBoxBuildContext context)
    {
        return new JsonObject
        {
            ["cache_file"] = new JsonObject
            {
                ["enabled"] = true,
                ["path"] = context.CacheFilePath
            },
            ["clash_api"] = new JsonObject
            {
                ["external_controller"] = context.ClashApiController,
                ["secret"] = settings.ClashApiSecret,
                ["default_mode"] = settings.RouteMode.ToString().ToLowerInvariant()
            }
        };
    }

    private static JsonArray BuildRouteRuleSets(
        bool hasProxyNodes,
        bool hasCnDomainRuleSet,
        bool hasCnIpRuleSet,
        SingBoxBuildContext context)
    {
        var ruleSets = new JsonArray();
        if (!hasProxyNodes)
        {
            return ruleSets;
        }

        if (hasCnDomainRuleSet)
        {
            ruleSets.Add(BuildRuleSet("cn-domain", context.CnDomainRuleSetPath));
        }

        if (hasCnIpRuleSet)
        {
            ruleSets.Add(BuildRuleSet("cn-ip", context.CnIpRuleSetPath));
        }

        return ruleSets;
    }

    private static JsonObject BuildRuleSet(string tag, string path)
    {
        return new JsonObject
        {
            ["type"] = "local",
            ["tag"] = tag,
            ["format"] = "binary",
            ["path"] = path
        };
    }

    private static string GetNodeTag(NodeProfile node) => SingBoxTags.GetNodeTag(node);
}
