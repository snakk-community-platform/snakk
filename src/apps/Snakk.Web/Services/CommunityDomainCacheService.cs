namespace Snakk.Web.Services;

using Microsoft.Extensions.Caching.Hybrid;

public record CommunityDomainLookupResult(
    bool Found,
    string? CommunitySlug,
    string? CommunityName = null,
    string? Timezone = null);

public interface ICommunityDomainCacheService
{
    Task<CommunityDomainLookupResult> GetCommunitySlugForDomainAsync(string domain);
    Task InvalidateDomainAsync(string domain);
}

public class CommunityDomainCacheService : ICommunityDomainCacheService
{
    private readonly HybridCache _cache;
    private readonly SnakkApiClient _apiClient;
    private readonly ILogger<CommunityDomainCacheService> _logger;
    private readonly HashSet<string> _primaryDomains;
    private readonly HybridCacheEntryOptions _cacheOptions;

    public CommunityDomainCacheService(
        HybridCache cache,
        SnakkApiClient apiClient,
        IConfiguration configuration,
        ILogger<CommunityDomainCacheService> logger)
    {
        _cache = cache;
        _apiClient = apiClient;
        _logger = logger;

        var primaryDomainsConfig = configuration.GetSection("Snakk:PrimaryDomains").Get<string[]>() ?? [];
        _primaryDomains = new HashSet<string>(primaryDomainsConfig, StringComparer.OrdinalIgnoreCase);

        _cacheOptions = new HybridCacheEntryOptions
        {
            Expiration = TimeSpan.FromMinutes(configuration.GetValue("Snakk:DomainCache:ExpirationMinutes", 15))
        };
    }

    public Task<CommunityDomainLookupResult> GetCommunitySlugForDomainAsync(string domain)
    {
        domain = NormalizeDomain(domain);

        if (_primaryDomains.Contains(domain))
            return Task.FromResult(new CommunityDomainLookupResult(false, null));

        return _cache.GetOrCreateAsync(
            $"domain:{domain}",
            cancel => new ValueTask<CommunityDomainLookupResult>(FetchAsync(domain, cancel)),
            _cacheOptions).AsTask();
    }

    private async Task<CommunityDomainLookupResult> FetchAsync(string domain, CancellationToken ct)
    {
        _logger.LogDebug("Domain cache miss for {Domain}, fetching from API", domain);
        try
        {
            var community = await _apiClient.GetCommunityByDomainAsync(domain);
            if (community is not null)
            {
                _logger.LogInformation("Cached domain {Domain} -> community {Slug} ({Name})", domain, community.Slug, community.Name);
                return new CommunityDomainLookupResult(true, community.Slug, community.Name,
                    community.HasTimezone ? community.Timezone : null);
            }

            _logger.LogDebug("Negative cache for domain {Domain}", domain);
            return new CommunityDomainLookupResult(false, null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to lookup domain {Domain} from API", domain);
            return new CommunityDomainLookupResult(false, null);
        }
    }

    public async Task InvalidateDomainAsync(string domain)
    {
        domain = NormalizeDomain(domain);
        await _cache.RemoveAsync($"domain:{domain}");
        _logger.LogInformation("Invalidated cache for domain {Domain}", domain);
    }

    private static string NormalizeDomain(string domain)
    {
        var colonIndex = domain.IndexOf(':');
        if (colonIndex > 0)
            domain = domain[..colonIndex];
        return domain.ToLowerInvariant();
    }
}
