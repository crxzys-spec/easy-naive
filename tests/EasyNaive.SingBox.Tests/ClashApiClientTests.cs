using System.Net;
using System.Net.Http.Headers;
using System.Text;
using EasyNaive.SingBox.ClashApi;
using Xunit;

namespace EasyNaive.SingBox.Tests;

public sealed class ClashApiClientTests
{
    [Fact]
    public async Task GetSelectedOutboundAsync_ParsesNowField()
    {
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"name":"auto","now":"node-node-b"}""", Encoding.UTF8, "application/json")
            });
        using var httpClient = new HttpClient(handler);
        using var client = new ClashApiClient(httpClient);

        var selected = await client.GetSelectedOutboundAsync("127.0.0.1:9090", "secret", "auto");

        Assert.Equal("node-node-b", selected);
        Assert.Equal(HttpMethod.Get, handler.LastMethod);
        Assert.Equal("http://127.0.0.1:9090/proxies/auto", handler.LastUri);
        Assert.Equal("Bearer", handler.LastAuthorization?.Scheme);
        Assert.Equal("secret", handler.LastAuthorization?.Parameter);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        public HttpMethod? LastMethod { get; private set; }

        public string? LastUri { get; private set; }

        public AuthenticationHeaderValue? LastAuthorization { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastMethod = request.Method;
            LastUri = request.RequestUri?.ToString();
            LastAuthorization = request.Headers.Authorization;
            return Task.FromResult(_handler(request));
        }
    }
}
