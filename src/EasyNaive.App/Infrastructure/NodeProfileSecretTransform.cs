using EasyNaive.Core.Models;
using EasyNaive.Platform.Windows.DataProtection;

namespace EasyNaive.App.Infrastructure;

internal sealed class NodeProfileSecretTransform : IJsonFileStoreTransform<List<NodeProfile>>
{
    private readonly IStringProtector _protector;

    public NodeProfileSecretTransform(IStringProtector protector)
    {
        _protector = protector;
    }

    public List<NodeProfile> AfterLoad(List<NodeProfile> value)
    {
        return value.Select(CloneWithUnprotectedPassword).ToList();
    }

    public List<NodeProfile> BeforeSave(List<NodeProfile> value)
    {
        return value.Select(CloneWithProtectedPassword).ToList();
    }

    private NodeProfile CloneWithProtectedPassword(NodeProfile source)
    {
        var node = Clone(source);
        node.Password = _protector.Protect(node.Password);
        return node;
    }

    private NodeProfile CloneWithUnprotectedPassword(NodeProfile source)
    {
        var node = Clone(source);
        node.Password = _protector.Unprotect(node.Password);
        return node;
    }

    private static NodeProfile Clone(NodeProfile source)
    {
        return new NodeProfile
        {
            Id = source.Id,
            SubscriptionId = source.SubscriptionId,
            Name = source.Name,
            Group = source.Group,
            Server = source.Server,
            ServerPort = source.ServerPort,
            Username = source.Username,
            Password = source.Password,
            TlsServerName = source.TlsServerName,
            UseQuic = source.UseQuic,
            UseUdpOverTcp = source.UseUdpOverTcp,
            Enabled = source.Enabled,
            SortOrder = source.SortOrder,
            Remark = source.Remark
        };
    }
}
