using EasyNaive.Core.Models;
using EasyNaive.Platform.Windows.DataProtection;

namespace EasyNaive.App.Infrastructure;

internal sealed class SubscriptionProfileSecretTransform : IJsonFileStoreTransform<List<SubscriptionProfile>>
{
    private readonly IStringProtector _protector;

    public SubscriptionProfileSecretTransform(IStringProtector protector)
    {
        _protector = protector;
    }

    public List<SubscriptionProfile> AfterLoad(List<SubscriptionProfile> value)
    {
        return value.Select(CloneWithUnprotectedUrl).ToList();
    }

    public List<SubscriptionProfile> BeforeSave(List<SubscriptionProfile> value)
    {
        return value.Select(CloneWithProtectedUrl).ToList();
    }

    private SubscriptionProfile CloneWithProtectedUrl(SubscriptionProfile source)
    {
        var subscription = Clone(source);
        subscription.Url = _protector.Protect(subscription.Url);
        return subscription;
    }

    private SubscriptionProfile CloneWithUnprotectedUrl(SubscriptionProfile source)
    {
        var subscription = Clone(source);
        subscription.Url = _protector.Unprotect(subscription.Url);
        return subscription;
    }

    private static SubscriptionProfile Clone(SubscriptionProfile source)
    {
        return new SubscriptionProfile
        {
            Id = source.Id,
            Name = source.Name,
            Url = source.Url,
            Enabled = source.Enabled,
            ImportedNodeCount = source.ImportedNodeCount,
            LastUpdated = source.LastUpdated,
            LastRefreshStarted = source.LastRefreshStarted,
            LastRefreshFinished = source.LastRefreshFinished,
            LastRefreshDurationMilliseconds = source.LastRefreshDurationMilliseconds,
            LastError = source.LastError
        };
    }
}
