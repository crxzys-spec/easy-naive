using EasyNaive.App.Infrastructure;
using EasyNaive.Core.Enums;
using EasyNaive.Core.Models;
using EasyNaive.Platform.Windows.DataProtection;
using Xunit;

namespace EasyNaive.App.Tests;

public sealed class AppSettingsSecretTransformTests : IDisposable
{
    private readonly string _tempDirectory;

    public AppSettingsSecretTransformTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "EasyNaive.App.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public void LoadOrCreate_WhenClashApiSecretIsPlaintext_ReturnsPlaintextForRuntime()
    {
        var path = Path.Combine(_tempDirectory, "settings.json");
        File.WriteAllText(path, """
                                 {
                                   "CaptureMode": "Proxy",
                                   "RouteMode": "Rule",
                                   "NodeMode": "Manual",
                                   "ProxyMixedPort": 2080,
                                   "ClashApiPort": 9090,
                                   "ClashApiSecret": "plain-secret",
                                   "EnableMinimizeToTray": true,
                                   "EnableTunStrictRoute": true,
                                   "LogLevel": "info"
                                 }
                                 """);
        var store = CreateSettingsStore(path);

        var settings = store.LoadOrCreate(AppSettings.CreateDefault);

        Assert.Equal("plain-secret", settings.ClashApiSecret);
    }

    [Fact]
    public void Save_ProtectsClashApiSecretOnDisk()
    {
        var path = Path.Combine(_tempDirectory, "settings.json");
        var store = CreateSettingsStore(path);
        var settings = new AppSettings
        {
            CaptureMode = CaptureMode.Tun,
            RouteMode = RouteMode.Global,
            NodeMode = NodeMode.Auto,
            ProxyMixedPort = 10808,
            ClashApiPort = 19090,
            ClashApiSecret = "plain-secret"
        };

        store.Save(settings);

        var json = File.ReadAllText(path);
        Assert.DoesNotContain("plain-secret", json, StringComparison.Ordinal);
        Assert.Contains(FakeStringProtector.Prefix, json, StringComparison.Ordinal);
        Assert.Equal("plain-secret", settings.ClashApiSecret);
    }

    [Fact]
    public void LoadOrCreate_WhenClashApiSecretIsProtected_ReturnsUnprotectedSecret()
    {
        var path = Path.Combine(_tempDirectory, "settings.json");
        var store = CreateSettingsStore(path);
        store.Save(new AppSettings
        {
            ClashApiSecret = "plain-secret"
        });

        var loaded = store.LoadOrCreate(AppSettings.CreateDefault);

        Assert.Equal("plain-secret", loaded.ClashApiSecret);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }

    private static JsonFileStore<AppSettings> CreateSettingsStore(string path)
    {
        return new JsonFileStore<AppSettings>(
            path,
            new AppSettingsSecretTransform(new FakeStringProtector()));
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
