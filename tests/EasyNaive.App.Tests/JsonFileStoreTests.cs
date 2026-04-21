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

    [Fact]
    public void SaveAndLoad_AppSessionState_PreservesSystemProxySnapshot()
    {
        var path = Path.Combine(_tempDirectory, "app-state.json");
        var store = new JsonFileStore<AppSessionState>(path);
        var capturedAt = new DateTimeOffset(2026, 4, 21, 12, 30, 0, TimeSpan.Zero);

        store.Save(new AppSessionState
        {
            SystemProxySnapshot = new SystemProxySnapshot
            {
                ProxyEnabled = true,
                ProxyServerExists = true,
                ProxyServer = "http=127.0.0.1:8888",
                ManagedPort = 2080,
                CapturedAt = capturedAt
            }
        });

        var loaded = store.LoadOrCreate(() => new AppSessionState());

        Assert.NotNull(loaded.SystemProxySnapshot);
        Assert.True(loaded.SystemProxySnapshot.ProxyEnabled);
        Assert.True(loaded.SystemProxySnapshot.ProxyServerExists);
        Assert.Equal("http=127.0.0.1:8888", loaded.SystemProxySnapshot.ProxyServer);
        Assert.Equal(2080, loaded.SystemProxySnapshot.ManagedPort);
        Assert.Equal(capturedAt, loaded.SystemProxySnapshot.CapturedAt);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }
}
