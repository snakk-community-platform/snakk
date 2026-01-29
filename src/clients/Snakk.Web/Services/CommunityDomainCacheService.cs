namespace Snakk.Web.Services;

using Microsoft.Extensions.Caching.Memory;

/// <summary>
/// Cached lookup result for a domain.
/// </summary>
public record CommunityDomainLookupResult(
    bool Found,
    string? CommunitySlug,
    string? CommunityName = null);

/// <summary>
/// Service that caches domain -> community slug mappings using IMemoryCache.
/// </summary>
public interface ICommunityDomainCacheService
{
    /// <summary>
    /// Looks up the community slug for a custom domain.
    /// Returns cached result if available, otherwise fetches from API and caches.
    /// </summary>
    Task<CommunityDomainLookupResult> GetCommunitySlugForDomainAsync(string domain);

    /// <summary>
    /// Invalidates the cache entry for a domain.
    /// </summary>
    void InvalidateDomain(string domain);
}

/// <summary>
/// Implementation of ICommunityDomainCacheService using IMemoryCache.
/// </summary>
public class CommunityDomainCacheService : ICommunityDomainCacheService
{
    private readonly IMemoryCache _cache;
    private readonly SnakkApiClient _apiClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CommunityDomainCacheService> _logger;
    private readonly HashSet<string> _primaryDomains;
    private readonly TimeSpan _cacheExpiration;
    private readonly TimeSpan _negativeCacheExpiration;

    private const string CacheKeyPrefix = "domain:";

    public CommunityDomainCacheService(
        IMemoryCache cache,
        SnakkApiClient apiClient,
        IConfiguration configuration,
        ILogger<CommunityDomainCacheService> logger)
    {
        _cache = cache;
        _apiClient = apiClient;
        _configuration = configuration;
        _logger = logger;

        // Load primary domains from configuration (these are the main platform domains)
        var primaryDomainsConfig = configuration.GetSection("Snakk:PrimaryDomains").Get<string[]>() ?? [];
        _primaryDomains = new HashSet<string>(primaryDomainsConfig, StringComparer.OrdinalIgnoreCase);

        // Cache expiration settings
        _cacheExpiration = TimeSpan.FromMinutes(
            configuration.GetValue("Snakk:DomainCache:ExpirationMinutes", 15));
        _negativeCacheExpiration = TimeSpan.FromMinutes(
            configuration.GetValue("Snakk:DomainCache:NegativeExpirationMinutes", 5));
    }

    public async Task<CommunityDomainLookupResult> GetCommunitySlugForDomainAsync(string domain)
    {
        // Normalize domain (remove port, lowercase)
        domain = NormalizeDomain(domain);

        // Primary domains are not custom domains
        if (_primaryDomains.Contains(domain))
        {
            return new CommunityDomainLookupResult(false, null);
        }

        var cacheKey = $"{CacheKeyPrefix}{domain}";

        // Try to get from cache
        if (_cache.TryGetValue(cacheKey, out CommunityDomainLookupResult? cachedResult))
        {
            _logger.LogDebug("Domain cache hit for {Domain}: {Result}", domain, cachedResult);
            return cachedResult!;
        }

        // Cache miss - fetch from API
        _logger.LogDebug("Domain cache miss for {Domain}, fetching from API", domain);

        try
        {
            var community = await _apiClient.GetCommunityByDomainAsync(domain);

            if (community != null)
            {
                var result = new CommunityDomainLookupResult(true, community.Slug, community.Name);

                // Cache the result
                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(_cacheExpiration)
                    .SetSize(1);

                _cache.Set(cacheKey, result, cacheOptions);
                _logger.LogInformation("Cached domain {Domain} -> community {Slug} ({Name})", domain, community.Slug, community.Name);

                return result;
            }
            else
            {
                // Negative cache - domain not found
                var result = new CommunityDomainLookupResult(false, null);

                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(_negativeCacheExpiration)
                    .SetSize(1);

                _cache.Set(cacheKey, result, cacheOptions);
                _logger.LogDebug("Negative cache for domain {Domain}", domain);

                return result;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to lookup domain {Domain} from API", domain);
            // Don't cache failures - return not found
            return new CommunityDomainLookupResult(false, null);
        }
    }

    public void InvalidateDomain(string domain)
    {
        domain = NormalizeDomain(domain);
        var cacheKey = $"{CacheKeyPrefix}{domain}";
        _cache.Remove(cacheKey);
        _logger.LogInformation("Invalidated cache for domain {Domain}", domain);
    }

    private static string NormalizeDomain(string domain)
    {
        // Remove port if present
        var colonIndex = domain.IndexOf(':');
        if (colonIndex > 0)
        {
            domain = domain[..colonIndex];
        }

        return domain.ToLowerInvariant();
    }
}
