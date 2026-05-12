using Microsoft.Extensions.Caching.Memory;
using Snakk.Web.Middleware;

namespace Snakk.Web.Services;

public record CacheResult<T>(T? Value, string Source) where T : class;

public interface IPrefetchCacheService
{
    /// <summary>
    /// Fire-and-forget: starts the factory immediately and stores the Task in cache.
    /// Called by page OnGetAsync to pre-warm data before the HTMX request arrives.
    /// </summary>
    void Prefetch<T>(string cacheKey, Func<Task<T>> factory, TimeSpan? ttl = null);

    /// <summary>
    /// Tries to consume a prefetched Task (with timeout), falls through to shared cache on miss.
    /// Called by sidebar partial endpoints. Returns result with source tag ("prefetch", "cache", or "api").
    /// </summary>
    Task<CacheResult<T>> GetOrFetchAsync<T>(string cacheKey, Func<Task<T?>> factory, TimeSpan? timeout = null, TimeSpan? sharedTtl = null) where T : class;

    /// <summary>
    /// Request coalescing: ensures only one factory invocation under concurrent access.
    /// Uses Lazy&lt;Task&gt; for atomicity. Longer TTL (30s default).
    /// </summary>
    Task<T?> GetOrCreateSharedAsync<T>(string cacheKey, Func<Task<T?>> factory, TimeSpan? ttl = null) where T : class;

    /// <summary>
    /// Synchronously checks if cached data is available (shared or prefetch cache).
    /// Returns the data if a completed Task exists, null otherwise. Zero latency.
    /// Used by page models to inline sidebar content when cache is warm.
    /// </summary>
    T? TryGetCached<T>(string cacheKey) where T : class;

    /// <summary>
    /// Checks cache for data. If warm, returns it. If cold, fires a background prefetch
    /// and returns null. Combines TryGetCached + Prefetch into a single call.
    /// </summary>
    T? ResolveOrPrefetch<T>(string cacheKey, Func<Task<T?>> factory) where T : class;

    /// <summary>
    /// Checks cache, maps the result if warm, fires prefetch if cold.
    /// Returns the mapped result or null — one-liner per sidebar section.
    /// </summary>
    TResult? ResolveOrPrefetch<T, TResult>(string cacheKey, Func<Task<T?>> factory, Func<T, TResult> mapper)
        where T : class where TResult : class;
}

public class PrefetchCacheService(
    IMemoryCache cache,
    ILogger<PrefetchCacheService> logger,
    IConfiguration configuration,
    IHttpContextAccessor httpContextAccessor) : IPrefetchCacheService
{
    private readonly IMemoryCache _cache = cache;
    private readonly ILogger<PrefetchCacheService> _logger = logger;
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
    private readonly TimeSpan _defaultPrefetchTtl =
        TimeSpan.FromSeconds(configuration.GetValue("PrefetchCache:PrefetchTtlSeconds", 5));

    private readonly TimeSpan _defaultSharedTtl =
        TimeSpan.FromSeconds(configuration.GetValue("PrefetchCache:SharedTtlSeconds", 10));

    private readonly TimeSpan _defaultPrefetchTimeout =
        TimeSpan.FromMilliseconds(configuration.GetValue("PrefetchCache:PrefetchTimeoutMs", 1000));

    public void Prefetch<T>(string cacheKey, Func<Task<T>> factory, TimeSpan? ttl = null)
    {
        var key = $"prefetch:{cacheKey}";

        // Log prefetch initiation to Server-Timing (HttpContext is available here, on the request thread)
        var collector = _httpContextAccessor.HttpContext?.Items["ServerTiming"] as ServerTimingCollector;
        var offsetMs = collector?.GetOffsetMs();
        collector?.Add("prefetch", 0, $"prefetch {cacheKey}", offsetMs);

        var task = Task.Run(async () =>
        {
            try
            {
                return await factory();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Prefetch failed for {CacheKey}", cacheKey);
                throw;
            }
        });

        _cache.Set(key, task, ttl ?? _defaultPrefetchTtl);
    }

    public async Task<CacheResult<T>> GetOrFetchAsync<T>(
        string cacheKey,
        Func<Task<T?>> factory,
        TimeSpan? timeout = null,
        TimeSpan? sharedTtl = null) where T : class
    {
        var prefetchKey = $"prefetch:{cacheKey}";

        // Try to consume a prefetched task
        if (_cache.TryGetValue<Task<T>>(prefetchKey, out var prefetchedTask) && prefetchedTask is not null)
        {
            try
            {
                var collector = _httpContextAccessor.HttpContext?.Items["ServerTiming"] as ServerTimingCollector;
                var offsetMs = collector?.GetOffsetMs();
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var effectiveTimeout = timeout ?? _defaultPrefetchTimeout;
                var completed = await Task.WhenAny(prefetchedTask, Task.Delay(effectiveTimeout));

                if (completed == prefetchedTask && prefetchedTask.IsCompletedSuccessfully)
                {
                    sw.Stop();
                    AddServerTiming("cache", sw.ElapsedMilliseconds, $"prefetch:{cacheKey}", offsetMs);
                    return new CacheResult<T>(prefetchedTask.Result, "prefetch");
                }

                // Timed out or faulted — remove and fall through
                _cache.Remove(prefetchKey);
            }
            catch
            {
                _cache.Remove(prefetchKey);
            }
        }

        // Fall through to shared coalesced cache
        var sharedKey = $"shared:{cacheKey}";
        var isSharedHit = _cache.TryGetValue(sharedKey, out _);

        var collector2 = _httpContextAccessor.HttpContext?.Items["ServerTiming"] as ServerTimingCollector;
        var offsetMs2 = collector2?.GetOffsetMs();
        var sw2 = System.Diagnostics.Stopwatch.StartNew();
        var value = await GetOrCreateSharedAsync(cacheKey, factory, sharedTtl);
        sw2.Stop();
        var source = isSharedHit ? "cache" : "api";
        AddServerTiming("cache", sw2.ElapsedMilliseconds, $"{source}:{cacheKey}", offsetMs2);

        return new CacheResult<T>(value, source);
    }

    public async Task<T?> GetOrCreateSharedAsync<T>(
        string cacheKey,
        Func<Task<T?>> factory,
        TimeSpan? ttl = null) where T : class
    {
        var sharedKey = $"shared:{cacheKey}";
        var effectiveTtl = ttl ?? _defaultSharedTtl;

        var lazy = _cache.GetOrCreate(sharedKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = effectiveTtl;
            return new Lazy<Task<T?>>(() => factory());
        });

        try
        {
            return await lazy!.Value;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Shared cache factory failed for {CacheKey}", cacheKey);
            // Remove faulted entry so next caller retries
            _cache.Remove(sharedKey);
            return null;
        }
    }

    public T? TryGetCached<T>(string cacheKey) where T : class
    {
        // Check shared cache first (longer TTL, most likely to be warm)
        var sharedKey = $"shared:{cacheKey}";
        if (_cache.TryGetValue<Lazy<Task<T?>>>(sharedKey, out var lazy)
            && lazy?.IsValueCreated == true
            && lazy.Value.IsCompletedSuccessfully)
        {
            return lazy.Value.Result;
        }

        // Check prefetch cache (shorter TTL, from current or recent request)
        var prefetchKey = $"prefetch:{cacheKey}";
        if (_cache.TryGetValue<Task<T>>(prefetchKey, out var task)
            && task is { IsCompletedSuccessfully: true })
        {
            return task.Result;
        }

        return null;
    }

    public T? ResolveOrPrefetch<T>(string cacheKey, Func<Task<T?>> factory) where T : class
    {
        var cached = TryGetCached<T>(cacheKey);
        if (cached is not null)
            return cached;

        try { Prefetch(cacheKey, async () => (await factory())!); } catch { }
        return null;
    }

    public TResult? ResolveOrPrefetch<T, TResult>(string cacheKey, Func<Task<T?>> factory, Func<T, TResult> mapper)
        where T : class where TResult : class
    {
        var data = ResolveOrPrefetch(cacheKey, factory);
        return data is not null ? mapper(data) : null;
    }

    private void AddServerTiming(string name, double durationMs, string? description = null, long? offsetMs = null)
    {
        var collector = _httpContextAccessor.HttpContext?.Items["ServerTiming"] as ServerTimingCollector;
        collector?.Add(name, durationMs, description, offsetMs);
    }
}
