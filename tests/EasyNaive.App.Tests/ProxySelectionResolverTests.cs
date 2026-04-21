using EasyNaive.App.Session;
using EasyNaive.Core.Models;
using Xunit;

namespace EasyNaive.App.Tests;

public sealed class ProxySelectionResolverTests
{
    private static readonly NodeProfile[] Nodes =
    [
        new()
        {
            Id = "node-a",
            Name = "A",
            Server = "a.example"
        },
        new()
        {
            Id = "node-b",
            Name = "B",
            Server = "b.example"
        }
    ];

    [Fact]
    public void ResolveCurrentRealNodeId_WhenProxySelectorUsesManual_ReturnsManualNode()
    {
        var nodeId = ProxySelectionResolver.ResolveCurrentRealNodeId("manual", "node-node-a", "node-node-b", Nodes);

        Assert.Equal("node-a", nodeId);
    }

    [Fact]
    public void ResolveCurrentRealNodeId_WhenProxySelectorUsesAuto_ReturnsAutoNode()
    {
        var nodeId = ProxySelectionResolver.ResolveCurrentRealNodeId("auto", "node-node-a", "node-node-b", Nodes);

        Assert.Equal("node-b", nodeId);
    }

    [Fact]
    public void ResolveCurrentRealNodeId_WhenProxySelectorPointsDirectlyToNode_ReturnsThatNode()
    {
        var nodeId = ProxySelectionResolver.ResolveCurrentRealNodeId("node-node-b", null, null, Nodes);

        Assert.Equal("node-b", nodeId);
    }

    [Fact]
    public void ResolveCurrentRealNodeId_WhenSelectionIsUnknown_ReturnsEmptyString()
    {
        var nodeId = ProxySelectionResolver.ResolveCurrentRealNodeId("auto", null, "node-node-x", Nodes);

        Assert.Equal(string.Empty, nodeId);
    }
}
