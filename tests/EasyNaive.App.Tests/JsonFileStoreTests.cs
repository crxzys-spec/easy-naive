using EasyNaive.App.Infrastructure;
using EasyNaive.Core.Enums;
using EasyNaive.Core.Models;
using Xunit;

namespace EasyNaive.App.Tests;

public sealed class JsonFileStoreTests : IDisposable
{
    private readonly string _tempDirectory;

    public JsonFileStoreTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "EasyNaive.App.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public void LoadOrCreate_WhenFileMissing_CreatesAndPersistsJson()
    {
        var path = Path.Combine(_tempDirectory, "settings.json");
        var store = new JsonFileStore<AppSettings>(path);

        var settings = store.LoadOrCreate(() => new AppSettings
        {
            CaptureMode = CaptureMode.Proxy,
            RouteMode = RouteMode.Rule,
            NodeMode = NodeMode.Manual,
            ProxyMixedPort = 2080,
            ClashApiPort = 9090,
            ClashApiSecret = "secret"
        });

        var json = File.ReadAllText(path);

        Assert.True(File.Exists(path));
        Assert.Equal(CaptureMode.Proxy, settings.CaptureMode);
        Assert.Contains("\"CaptureMode\": \"Proxy\"", json, StringComparison.Ordinal);
        Assert.Contains("\"RouteMode\": \"Rule\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void LoadOrCreate_WhenFileExists_ReturnsStoredValue()
    {
        var path = Path.Combine(_tempDirectory, "settings.json");
        var store = new JsonFileStore<AppSettings>(path);

        store.Save(new AppSettings
        {
            CaptureMode = CaptureMode.Tun,
            RouteMode = RouteMode.Global,
            NodeMode = NodeMode.Auto,
            ProxyMixedPort = 10808,
            ClashApiPort = 19090,
            ClashApiSecret = "persisted-secret"
        });

        var loaded = store.LoadOrCreate(AppSettings.CreateDefault);

        Assert.Equal(CaptureMode.Tun, loaded.CaptureMode);
        Assert.Equal(RouteMode.Global, loaded.RouteMode);
        Assert.Equal(NodeMode.Auto, loaded.NodeMode);
        Assert.Equal(10808, loaded.ProxyMixedPort);
        Assert.Equal(19090, loaded.ClashApiPort);
        Assert.Equal("persisted-secret", loaded.ClashApiSecret);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }
}
