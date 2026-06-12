using Microsoft.Extensions.Caching.Memory;

namespace Snakk.Web.Services;

/// <summary>
/// Dedicated bounded cache for partial-render results (DiscussionItem, Post).
/// Isolated from the shared IMemoryCache so that a SizeLimit can be enforced without
/// affecting WebOptimizer / output-cache internals that use IMemoryCache without Size.
/// </summary>
public sealed class PartialRenderCache : IDisposable
{
    private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = 2048 });

    /// <summary>
    /// Returns the cached value for <paramref name="key"/>, or invokes
    /// <paramref name="factory"/> to produce it, caches it (Size = 1), and returns it.
    /// Stampede safety is intentionally omitted — the 10 s TTL and cheap factory make
    /// duplicate concurrent executions harmless and keeping this simple avoids lock overhead.
    /// </summary>
    public Task<T?> GetOrCreateAsync<T>(string key, Func<ICacheEntry, Task<T?>> factory)
        where T : class
    {
        return _cache.GetOrCreateAsync(key, async entry =>
        {
            entry.Size = 1;
            return await factory(entry);
        });
    }

    public void Dispose() => _cache.Dispose();
}
