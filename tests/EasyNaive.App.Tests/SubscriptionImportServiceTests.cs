using System.Text;
using EasyNaive.App.Subscriptions;
using EasyNaive.Core.Models;
using Xunit;

namespace EasyNaive.App.Tests;

public sealed class SubscriptionImportServiceTests
{
    private readonly SubscriptionProfile _subscription = new()
    {
        Id = "sub-1",
        Name = "My Subscription",
        Url = "https://example.com/sub.txt",
        Enabled = true
    };

    [Fact]
    public void ParseNodes_WhenGivenNaiveUriText_ReturnsNormalizedNode()
    {
        using var service = new SubscriptionImportService();

        var nodes = service.ParseNodes(
            _subscription,
            "naive://user:pass@example.com:443?group=HK&sni=edge.example#Node%201");

        var node = Assert.Single(nodes);
        Assert.Equal("Node 1", node.Name);
        Assert.Equal("HK", node.Group);
        Assert.Equal("example.com", node.Server);
        Assert.Equal(443, node.ServerPort);
        Assert.Equal("user", node.Username);
        Assert.Equal("pass", node.Password);
        Assert.Equal("edge.example", node.TlsServerName);
    }

    [Fact]
    public void ParseNodes_WhenGivenBase64Payload_DecodesAndParsesNodes()
    {
        using var service = new SubscriptionImportService();
        var raw = "naive://alice:secret@sg.example:8443?group=SG#Singapore";
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));

        var nodes = service.ParseNodes(_subscription, encoded);

        var node = Assert.Single(nodes);
        Assert.Equal("Singapore", node.Name);
        Assert.Equal("SG", node.Group);
        Assert.Equal("sg.example", node.Server);
        Assert.Equal(8443, node.ServerPort);
    }

    [Fact]
    public void ParseNodes_WhenGivenJsonArray_ParsesNaiveSpecificOptions()
    {
        using var service = new SubscriptionImportService();
        var payload = """
                      [
                        {
                          "type": "naive",
                          "name": "JP",
                          "server": "jp.example",
                          "server_port": 9443,
                          "username": "bob",
                          "password": "pwd",
                          "tls": { "server_name": "tls.example" },
                          "udp_over_tcp": { "enabled": true },
                          "enabled": true
                        }
                      ]
                      """;

        var nodes = service.ParseNodes(_subscription, payload);

        var node = Assert.Single(nodes);
        Assert.Equal("JP", node.Name);
        Assert.Equal("My Subscription", node.Group);
        Assert.Equal("jp.example", node.Server);
        Assert.Equal(9443, node.ServerPort);
        Assert.Equal("bob", node.Username);
        Assert.Equal("pwd", node.Password);
        Assert.Equal("tls.example", node.TlsServerName);
        Assert.True(node.UseUdpOverTcp);
    }
}
