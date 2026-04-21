using EasyNaive.App.Infrastructure;
using EasyNaive.Core.Models;
using EasyNaive.Platform.Windows.DataProtection;
using Xunit;

namespace EasyNaive.App.Tests;

public sealed class NodeProfileSecretTransformTests : IDisposable
{
    private readonly string _tempDirectory;

    public NodeProfileSecretTransformTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "EasyNaive.App.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public void LoadOrCreate_WhenNodePasswordIsPlaintext_ReturnsPlaintextForRuntime()
    {
        var path = Path.Combine(_tempDirectory, "nodes.json");
        File.WriteAllText(path, """
                                 [
                                   {
                                     "Id": "node-1",
                                     "Name": "Test",
                                     "Group": "Default",
                                     "Server": "example.com",
                                     "ServerPort": 443,
                                     "Username": "user",
                                     "Password": "plain-password",
                                     "TlsServerName": "example.com",
                                     "Enabled": true
                                   }
                                 ]
                                 """);
        var store = CreateNodeStore(path);

        var nodes = store.LoadOrCreate(() => new List<NodeProfile>());

        var node = Assert.Single(nodes);
        Assert.Equal("plain-password", node.Password);
    }

    [Fact]
    public void Save_ProtectsNodePasswordOnDisk()
    {
        var path = Path.Combine(_tempDirectory, "nodes.json");
        var store = CreateNodeStore(path);
        var nodes = new List<NodeProfile>
        {
            new()
            {
                Id = "node-1",
                Name = "Test",
                Server = "example.com",
                Username = "user",
                Password = "plain-password"
            }
        };

        store.Save(nodes);

        var json = File.ReadAllText(path);
        Assert.DoesNotContain("plain-password", json, StringComparison.Ordinal);
        Assert.Contains(FakeStringProtector.Prefix, json, StringComparison.Ordinal);
        Assert.Equal("plain-password", nodes[0].Password);
    }

    [Fact]
    public void LoadOrCreate_WhenNodePasswordIsProtected_ReturnsUnprotectedPassword()
    {
        var path = Path.Combine(_tempDirectory, "nodes.json");
        var store = CreateNodeStore(path);
        store.Save(new List<NodeProfile>
        {
            new()
            {
                Id = "node-1",
                Name = "Test",
                Server = "example.com",
                Password = "plain-password"
            }
        });

        var loaded = store.LoadOrCreate(() => new List<NodeProfile>());

        var node = Assert.Single(loaded);
        Assert.Equal("plain-password", node.Password);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }

    private static JsonFileStore<List<NodeProfile>> CreateNodeStore(string path)
    {
        return new JsonFileStore<List<NodeProfile>>(
            path,
            new NodeProfileSecretTransform(new FakeStringProtector()));
    }

    private sealed class FakeStringProtector : IStringProtector
    {
        public const string Prefix = "test-protected:";

        public bool IsProtected(string value)
        {
            return value.StartsWith(Prefix, StringComparison.Ordinal);
        }

        public string Protect(string value)
        {
            if (string.IsNullOrEmpty(value) || IsProtected(value))
            {
                return value;
            }

            return Prefix + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value));
        }

        public string Unprotect(string value)
        {
            if (string.IsNullOrEmpty(value) || !IsProtected(value))
            {
                return value;
            }

            var payload = value[Prefix.Length..];
            return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payload));
        }
    }
}
