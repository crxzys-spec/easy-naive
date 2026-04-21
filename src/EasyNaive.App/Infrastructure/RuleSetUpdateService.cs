using System.Net.Http.Headers;

namespace EasyNaive.App.Infrastructure;

internal sealed class RuleSetUpdateService : IDisposable
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromDays(7);

    private static readonly Uri[] CnDomainRuleSetUrls =
    [
        new("https://raw.githubusercontent.com/SagerNet/sing-geosite/rule-set/geosite-cn.srs")
    ];

    private static readonly Uri[] CnIpRuleSetUrls =
    [
        new("https://raw.githubusercontent.com/SagerNet/sing-geoip/rule-set/geoip-cn.srs")
    ];

    private readonly AppPaths _paths;
    private readonly HttpClient _httpClient;

    public RuleSetUpdateService(AppPaths paths)
    {
        _paths = paths;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("EasyNaive", "0.1"));
    }

    public Task<RuleSetUpdateSummary> EnsureAsync(CancellationToken cancellationToken = default)
    {
        return UpdateInternalAsync(force: false, failIfUnavailable: false, cancellationToken);
    }

    public Task<RuleSetUpdateSummary> UpdateAsync(CancellationToken cancellationToken = default)
    {
        return UpdateInternalAsync(force: true, failIfUnavailable: true, cancellationToken);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    private async Task<RuleSetUpdateSummary> UpdateInternalAsync(bool force, bool failIfUnavailable, CancellationToken cancellationToken)
    {
        _paths.EnsureDirectories();

        var domainResult = await UpdateRuleSetAsync(
            _paths.CnDomainRuleSetPath,
            CnDomainRuleSetUrls,
            "CN domain rule-set",
            force,
            failIfUnavailable,
            cancellationToken);

        var ipResult = await UpdateRuleSetAsync(
            _paths.CnIpRuleSetPath,
            CnIpRuleSetUrls,
            "CN IP rule-set",
            force,
            failIfUnavailable,
            cancellationToken);

        return new RuleSetUpdateSummary(domainResult, ipResult);
    }

    private async Task<RuleSetItemUpdateResult> UpdateRuleSetAsync(
        string destinationPath,
        IReadOnlyList<Uri> sourceUrls,
        string displayName,
        bool force,
        bool failIfUnavailable,
        CancellationToken cancellationToken)
    {
        var fileExists = File.Exists(destinationPath);
        var shouldRefresh = force || !fileExists || IsStale(destinationPath);
        if (!shouldRefresh)
        {
            return new RuleSetItemUpdateResult(displayName, destinationPath, Updated: false, Available: true);
        }

        Exception? lastError = null;
        foreach (var sourceUrl in sourceUrls)
        {
            try
            {
                await DownloadRuleSetAsync(sourceUrl, destinationPath, cancellationToken);
                return new RuleSetItemUpdateResult(displayName, destinationPath, Updated: true, Available: true);
            }
            catch (Exception ex)
            {
                lastError = ex;
            }
        }

        if (fileExists && !failIfUnavailable)
        {
            return new RuleSetItemUpdateResult(displayName, destinationPath, Updated: false, Available: true, Warning: lastError?.Message ?? "Download failed.");
        }

        throw new InvalidOperationException($"Failed to update {displayName}.", lastError);
    }

    private async Task DownloadRuleSetAsync(Uri sourceUrl, string destinationPath, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(sourceUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var tempPath = destinationPath + ".download";
        await using (var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken))
        await using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await responseStream.CopyToAsync(fileStream, cancellationToken);
        }

        var fileInfo = new FileInfo(tempPath);
        if (!fileInfo.Exists || fileInfo.Length == 0)
        {
            throw new InvalidOperationException($"Downloaded file from {sourceUrl} is empty.");
        }

        File.Copy(tempPath, destinationPath, overwrite: true);
        File.Delete(tempPath);
        File.SetLastWriteTimeUtc(destinationPath, DateTime.UtcNow);
    }

    private static bool IsStale(string path)
    {
        var lastWrite = File.GetLastWriteTimeUtc(path);
        return lastWrite <= DateTime.UtcNow.Subtract(RefreshInterval);
    }
}

internal sealed record RuleSetUpdateSummary(
    RuleSetItemUpdateResult DomainRuleSet,
    RuleSetItemUpdateResult IpRuleSet)
{
    public bool AnyUpdated => DomainRuleSet.Updated || IpRuleSet.Updated;

    public bool HasWarnings =>
        !string.IsNullOrWhiteSpace(DomainRuleSet.Warning) ||
        !string.IsNullOrWhiteSpace(IpRuleSet.Warning);

    public string ToDisplayText()
    {
        return string.Join(
            Environment.NewLine,
            BuildLine(DomainRuleSet),
            BuildLine(IpRuleSet));
    }

    private static string BuildLine(RuleSetItemUpdateResult item)
    {
        var status = item.Updated ? "updated" : "ready";
        var suffix = string.IsNullOrWhiteSpace(item.Warning) ? string.Empty : $" ({item.Warning})";
        return $"{item.DisplayName}: {status} - {item.Path}{suffix}";
    }
}

internal sealed record RuleSetItemUpdateResult(
    string DisplayName,
    string Path,
    bool Updated,
    bool Available,
    string Warning = "");
