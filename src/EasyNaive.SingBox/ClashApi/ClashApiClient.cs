using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace EasyNaive.SingBox.ClashApi;

public sealed class ClashApiClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public ClashApiClient(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? CreateLocalApiHttpClient();
        _ownsHttpClient = httpClient is null;
    }

    public async Task WaitUntilAvailableAsync(string controller, string secret, CancellationToken cancellationToken = default)
    {
        Exception? lastException = null;

        for (var attempt = 0; attempt < 15; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await GetConfigsAsync(controller, secret, cancellationToken);
                return;
            }
            catch (Exception ex) when (attempt < 14)
            {
                lastException = ex;
                await Task.Delay(TimeSpan.FromMilliseconds(300), cancellationToken);
            }
        }

        throw new InvalidOperationException("Clash API did not become available in time.", lastException);
    }

    public async Task SetModeAsync(string controller, string secret, string mode, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Patch, controller, "/configs", secret);
        request.Content = JsonContent.Create(new { mode });

        await SendNoContentAsync(request, cancellationToken);
    }

    public async Task CloseAllConnectionsAsync(string controller, string secret, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Delete, controller, "/connections", secret);
        await SendNoContentAsync(request, cancellationToken);
    }

    public async Task<string?> GetSelectedOutboundAsync(string controller, string secret, string proxyTag, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, controller, $"/proxies/{Uri.EscapeDataString(proxyTag)}", secret);
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<ClashProxyResponse>(cancellationToken: cancellationToken);
        return payload?.Now;
    }

    public async Task SelectOutboundAsync(string controller, string secret, string selectorTag, string outboundTag, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Put, controller, $"/proxies/{Uri.EscapeDataString(selectorTag)}", secret);
        request.Content = JsonContent.Create(new { name = outboundTag });

        await SendNoContentAsync(request, cancellationToken);
    }

    public async Task<int> TestProxyDelayAsync(string controller, string secret, string proxyTag, string url, int timeoutMs, CancellationToken cancellationToken = default)
    {
        var requestPath =
            $"/proxies/{Uri.EscapeDataString(proxyTag)}/delay?url={Uri.EscapeDataString(url)}&timeout={timeoutMs}";

        using var request = CreateRequest(HttpMethod.Get, controller, requestPath, secret);
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<ClashDelayResponse>(cancellationToken: cancellationToken);
        if (payload is null || payload.Delay <= 0)
        {
            throw new InvalidOperationException("Clash API returned an invalid delay response.");
        }

        return payload.Delay;
    }

    public async Task<ClashTrafficSnapshot> GetTrafficSnapshotAsync(string controller, string secret, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, controller, "/connections", secret);
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<ClashConnectionsResponse>(cancellationToken: cancellationToken);
        if (payload is null)
        {
            throw new InvalidOperationException("Clash API returned an empty traffic response.");
        }

        return new ClashTrafficSnapshot
        {
            UploadTotalBytes = payload.UploadTotal,
            DownloadTotalBytes = payload.DownloadTotal,
            ActiveConnections = payload.Connections?.Length ?? 0,
            MemoryBytes = payload.Memory
        };
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    private static HttpClient CreateLocalApiHttpClient()
    {
        var handler = new HttpClientHandler
        {
            UseProxy = false
        };

        return new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(3)
        };
    }

    private async Task<ClashConfigResponse> GetConfigsAsync(string controller, string secret, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, controller, "/configs", secret);
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<ClashConfigResponse>(cancellationToken: cancellationToken);
        return payload ?? throw new InvalidOperationException("Clash API returned an empty configuration response.");
    }

    private async Task SendNoContentAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string controller, string path, string secret)
    {
        var request = new HttpRequestMessage(method, BuildRequestUri(controller, path));

        if (!string.IsNullOrWhiteSpace(secret))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secret);
        }

        return request;
    }

    private static Uri BuildRequestUri(string controller, string path)
    {
        return new Uri($"http://{controller.TrimEnd('/')}{path}", UriKind.Absolute);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var message = string.IsNullOrWhiteSpace(body)
            ? $"Clash API request failed with {(int)response.StatusCode} {response.ReasonPhrase}."
            : $"Clash API request failed with {(int)response.StatusCode} {response.ReasonPhrase}: {body}";

        throw new InvalidOperationException(message);
    }

    private sealed class ClashConfigResponse
    {
        public string Mode { get; init; } = string.Empty;

        public IReadOnlyList<string> ModeList { get; init; } = Array.Empty<string>();
    }

    private sealed class ClashDelayResponse
    {
        public int Delay { get; init; }
    }

    private sealed class ClashConnectionsResponse
    {
        public long DownloadTotal { get; init; }

        public long UploadTotal { get; init; }

        public JsonElement[]? Connections { get; init; }

        public ulong Memory { get; init; }
    }

    private sealed class ClashProxyResponse
    {
        public string Name { get; init; } = string.Empty;

        public string? Now { get; init; }
    }
}
