using EasyNaive.App.Infrastructure;
using EasyNaive.Core.Models;
using EasyNaive.Platform.Windows.DataProtection;
using Xunit;

namespace EasyNaive.App.Tests;

public sealed class SubscriptionProfileSecretTransformTests : IDisposable
{
    private readonly string _tempDirectory;

    public SubscriptionProfileSecretTransformTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "EasyNaive.App.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public void LoadOrCreate_WhenSubscriptionUrlIsPlaintext_ReturnsPlaintextForRuntime()
    {
        var path = Path.Combine(_tempDirectory, "subscriptions.json");
        File.WriteAllText(path, """
                                 [
                                   {
                                     "Id": "sub-1",
                                     "Name": "Sub",
                                     "Url": "https://example.com/sub?token=secret",
                                     "Enabled": true,
                                     "ImportedNodeCount": 1,
                                     "LastError": ""
                                   }
                                 ]
                                 """);
        var store = CreateSubscriptionStore(path);

        var subscriptions = store.LoadOrCreate(() => new List<SubscriptionProfile>());

        var subscription = Assert.Single(subscriptions);
        Assert.Equal("https://example.com/sub?token=secret", subscription.Url);
    }

    [Fact]
    public void Save_ProtectsSubscriptionUrlOnDisk()
    {
        var path = Path.Combine(_tempDirectory, "subscriptions.json");
        var store = CreateSubscriptionStore(path);
        var subscriptions = new List<SubscriptionProfile>
        {
            new()
            {
                Id = "sub-1",
                Name = "Sub",
                Url = "https://example.com/sub?token=secret",
                Enabled = true
            }
        };

        store.Save(subscriptions);

        var json = File.ReadAllText(path);
        Assert.DoesNotContain("https://example.com/sub?token=secret", json, StringComparison.Ordinal);
        Assert.Contains(FakeStringProtector.Prefix, json, StringComparison.Ordinal);
        Assert.Equal("https://example.com/sub?token=secret", subscriptions[0].Url);
    }

    [Fact]
    public void LoadOrCreate_WhenSubscriptionUrlIsProtected_ReturnsUnprotectedUrl()
    {
        var path = Path.Combine(_tempDirectory, "subscriptions.json");
        var store = CreateSubscriptionStore(path);
        store.Save(new List<SubscriptionProfile>
        {
            new()
            {
                Id = "sub-1",
                Name = "Sub",
                Url = "https://example.com/sub?token=secret"
            }
        });

        var loaded = store.LoadOrCreate(() => new List<SubscriptionProfile>());

        var subscription = Assert.Single(loaded);
        Assert.Equal("https://example.com/sub?token=secret", subscription.Url);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }

    private static JsonFileStore<List<SubscriptionProfile>> CreateSubscriptionStore(string path)
    {
        return new JsonFileStore<List<SubscriptionProfile>>(
            path,
            new SubscriptionProfileSecretTransform(new FakeStringProtector()));
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
