using EasyNaive.App.Sharing;
using EasyNaive.App.Subscriptions;
using EasyNaive.Core.Models;
using Xunit;

namespace EasyNaive.App.Tests;

public sealed class NodeShareFormatterTests
{
    private readonly SubscriptionProfile _subscription = new()
    {
        Id = "manual",
        Name = "Imported",
        Url = "manual://import",
        Enabled = true
    };

    [Fact]
    public void FormatNode_WhenParsedAgain_PreservesNaiveFields()
    {
        var source = new NodeProfile
        {
            Name = "Hong Kong 1",
            Group = "Friends",
            Server = "edge.example.com",
            ServerPort = 8443,
            Username = "user:name",
            Password = "p@ss:word",
            TlsServerName = "tls.example.com",
            UseQuic = true,
            UseUdpOverTcp = true
        };

        using var importService = new SubscriptionImportService();
        var nodes = importService.ParseNodes(_subscription, NodeShareFormatter.FormatNode(source));

        var node = Assert.Single(nodes);
        Assert.Equal(source.Name, node.Name);
        Assert.Equal(source.Group, node.Group);
        Assert.Equal(source.Server, node.Server);
        Assert.Equal(source.ServerPort, node.ServerPort);
        Assert.Equal(source.Username, node.Username);
        Assert.Equal(source.Password, node.Password);
        Assert.Equal(source.TlsServerName, node.TlsServerName);
        Assert.True(node.UseQuic);
        Assert.True(node.UseUdpOverTcp);
    }

    [Fact]
    public void FormatNodes_UsesOneImportableLinePerNode()
    {
        var nodes = new[]
        {
            new NodeProfile
            {
                Name = "A",
                Group = "G",
                Server = "a.example",
                ServerPort = 443,
                Username = "u",
                Password = "p"
            },
            new NodeProfile
            {
                Name = "B",
                Group = "G",
                Server = "b.example",
                ServerPort = 8443,
                Username = "u",
                Password = "p"
            }
        };

        using var importService = new SubscriptionImportService();
        var parsed = importService.ParseNodes(_subscription, NodeShareFormatter.FormatNodes(nodes));

        Assert.Equal(2, parsed.Count);
        Assert.Equal("A", parsed[0].Name);
        Assert.Equal("B", parsed[1].Name);
    }
}
