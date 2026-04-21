using System.Net;
using EasyNaive.App.Infrastructure;
using Xunit;

namespace EasyNaive.App.Tests;

public sealed class RuleSetUpdateServiceTests : IDisposable
{
    private readonly string _tempDirectory;

    public RuleSetUpdateServiceTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "EasyNaive.App.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public async Task EnsureAsync_WhenRuleSetsAreUnavailableAndMissing_ReturnsWarningsWithoutThrowing()
    {
        var paths = AppPaths.CreateForTesting(_tempDirectory, _tempDirectory);
        using var httpClient = new HttpClient(new StatusCodeHandler(HttpStatusCode.BadGateway));
        using var service = new RuleSetUpdateService(
            paths,
            httpClient,
            [new Uri("https://example.invalid/cn-domain.srs")],
            [new Uri("https://example.invalid/cn-ip.srs")]);

        var summary = await service.EnsureAsync();

        Assert.False(summary.DomainRuleSet.Available);
        Assert.False(summary.IpRuleSet.Available);
        Assert.True(summary.HasWarnings);
        Assert.False(File.Exists(paths.CnDomainRuleSetPath));
        Assert.False(File.Exists(paths.CnIpRuleSetPath));
    }

    [Fact]
    public async Task UpdateAsync_WhenRuleSetsAreUnavailableAndMissing_Throws()
    {
        var paths = AppPaths.CreateForTesting(_tempDirectory, _tempDirectory);
        using var httpClient = new HttpClient(new StatusCodeHandler(HttpStatusCode.BadGateway));
        using var service = new RuleSetUpdateService(
            paths,
            httpClient,
            [new Uri("https://example.invalid/cn-domain.srs")],
            [new Uri("https://example.invalid/cn-ip.srs")]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync());
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private sealed class StatusCodeHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                RequestMessage = request
            });
        }
    }
}
