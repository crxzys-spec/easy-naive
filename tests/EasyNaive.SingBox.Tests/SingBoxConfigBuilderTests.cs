using System.Text.Json.Nodes;
using EasyNaive.Core.Enums;
using EasyNaive.Core.Models;
using EasyNaive.SingBox.Config;
using Xunit;

namespace EasyNaive.SingBox.Tests;

public sealed class SingBoxConfigBuilderTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly SingBoxConfigBuilder _builder = new();

    public SingBoxConfigBuilderTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "EasyNaive.SingBox.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public void BuildJson_WhenNoEnabledNodes_OmitsDnsAndRoutesDirectly()
    {
        var settings = CreateSettings();
        var context = CreateContext();
        var nodes = new[]
        {
            new NodeProfile
            {
                Id = "disabled-node",
                Name = "Disabled",
                Server = "disabled.example",
                Enabled = false
            }
        };

        var root = ParseRoot(_builder.BuildJson(settings, nodes, context));

        Assert.Null(root["dns"]);
        Assert.Equal("mixed", root["inbounds"]![0]!["type"]!.GetValue<string>());
        Assert.Equal(settings.ProxyMixedPort, root["inbounds"]![0]!["listen_port"]!.GetValue<int>());
        Assert.Equal("direct", root["route"]!["final"]!.GetValue<string>());
        Assert.Equal("rule", root["experimental"]!["clash_api"]!["default_mode"]!.GetValue<string>());
    }

    [Fact]
    public void BuildJson_WhenTunMode_UsesTunInboundAndEnablesUdpOverTcp()
    {
        var settings = CreateSettings();
        settings.CaptureMode = CaptureMode.Tun;
        settings.RouteMode = RouteMode.Global;

        var node = new NodeProfile
        {
            Id = "node-1",
            Name = "HK",
            Server = "hk.example",
            Username = "user",
            Password = "pass",
            UseUdpOverTcp = false
        };

        var root = ParseRoot(_builder.BuildJson(settings, new[] { node }, CreateContext()));
        var inbounds = root["inbounds"]!.AsArray();
        var outbounds = root["outbounds"]!.AsArray();
        var routeRules = root["route"]!["rules"]!.AsArray();

        Assert.Equal("tun", inbounds[0]!["type"]!.GetValue<string>());
        Assert.Contains(outbounds, outbound =>
            outbound?["tag"]?.GetValue<string>() == "node-node-1" &&
            outbound["udp_over_tcp"]?["enabled"]?.GetValue<bool>() == true);
        Assert.Contains(routeRules, rule => rule?["action"]?.GetValue<string>() == "hijack-dns");
        Assert.Equal("ipv4_only", root["dns"]!["strategy"]!.GetValue<string>());
        Assert.Equal("dns-proxy", root["dns"]!["final"]!.GetValue<string>());

        var dnsServers = root["dns"]!["servers"]!.AsArray();
        Assert.Contains(dnsServers, server =>
            server?["tag"]?.GetValue<string>() == "dns-direct" &&
            server["type"]?.GetValue<string>() == "udp" &&
            server["server"]?.GetValue<string>() == "223.5.5.5" &&
            server["server_port"]?.GetValue<int>() == 53);
        Assert.Contains(dnsServers, server =>
            server?["tag"]?.GetValue<string>() == "dns-proxy" &&
            server["type"]?.GetValue<string>() == "https" &&
            server["server"]?.GetValue<string>() == "1.1.1.1");
    }

    [Fact]
    public void BuildJson_WhenRuleSetsExist_AddsChinaRuleSetsAndDirectDnsFinal()
    {
        var settings = CreateSettings();
        settings.RouteMode = RouteMode.Direct;

        var context = CreateContext(createRuleSets: true);
        var node = new NodeProfile
        {
            Id = "node-2",
            Name = "JP",
            Server = "jp.example",
            Username = "user",
            Password = "pass"
        };

        var root = ParseRoot(_builder.BuildJson(settings, new[] { node }, context));
        var ruleSets = root["route"]!["rule_set"]!.AsArray();
        var routeRules = root["route"]!["rules"]!.AsArray();

        Assert.Contains(ruleSets, item => item?["tag"]?.GetValue<string>() == "cn-domain");
        Assert.Contains(ruleSets, item => item?["tag"]?.GetValue<string>() == "cn-ip");
        Assert.Contains(routeRules, rule => RuleContainsRuleSet(rule, "cn-domain"));
        Assert.Contains(routeRules, rule => RuleContainsRuleSet(rule, "cn-ip"));
        Assert.Equal("dns-direct", root["dns"]!["final"]!.GetValue<string>());

        var dnsServers = root["dns"]!["servers"]!.AsArray();
        Assert.Contains(dnsServers, server =>
            server?["tag"]?.GetValue<string>() == "dns-direct" &&
            server["type"]?.GetValue<string>() == "udp" &&
            server["server"]?.GetValue<string>() == "223.5.5.5" &&
            server["address"] is null);
        Assert.DoesNotContain(dnsServers, server => server?["address"] is not null);
        Assert.Equal("ipv4_only", root["dns"]!["strategy"]!.GetValue<string>());
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }

    private AppSettings CreateSettings()
    {
        return new AppSettings
        {
            CaptureMode = CaptureMode.Proxy,
            RouteMode = RouteMode.Rule,
            NodeMode = NodeMode.Manual,
            ProxyMixedPort = 2080,
            ClashApiPort = 9090,
            ClashApiSecret = "secret",
            LogLevel = "info"
        };
    }

    private SingBoxBuildContext CreateContext(bool createRuleSets = false)
    {
        var cnDomainPath = Path.Combine(_tempDirectory, "cn-domain.srs");
        var cnIpPath = Path.Combine(_tempDirectory, "cn-ip.srs");

        if (createRuleSets)
        {
            File.WriteAllText(cnDomainPath, "cn-domain");
            File.WriteAllText(cnIpPath, "cn-ip");
        }

        return new SingBoxBuildContext
        {
            CacheFilePath = Path.Combine(_tempDirectory, "cache.db"),
            CnDomainRuleSetPath = cnDomainPath,
            CnIpRuleSetPath = cnIpPath,
            ClashApiController = "127.0.0.1:9090"
        };
    }

    private static JsonObject ParseRoot(string json)
    {
        return JsonNode.Parse(json)?.AsObject() ?? throw new InvalidOperationException("Failed to parse JSON output.");
    }

    private static bool RuleContainsRuleSet(JsonNode? rule, string tag)
    {
        var ruleSet = rule?["rule_set"]?.AsArray();
        return ruleSet is not null && ruleSet.Any(item => string.Equals(item?.GetValue<string>(), tag, StringComparison.Ordinal));
    }
}
