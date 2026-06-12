namespace Snakk.Web.Services;

using Microsoft.Extensions.Caching.Hybrid;

public interface IFollowedSpacesCacheService
{
    Task<List<string>> GetAsync(string userId, Func<CancellationToken, Task<List<string>>> factory, CancellationToken ct = default);
    ValueTask InvalidateAsync(string userId, CancellationToken ct = default);
}

public class FollowedSpacesCacheService(HybridCache cache) : IFollowedSpacesCacheService
{
    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(2),
        LocalCacheExpiration = TimeSpan.FromMinutes(2)
    };

    private static string Key(string userId) => $"followed-spaces:{userId}";

    public async Task<List<string>> GetAsync(string userId, Func<CancellationToken, Task<List<string>>> factory, CancellationToken ct = default)
    {
        return await cache.GetOrCreateAsync<List<string>>(
            Key(userId),
            async token => await factory(token),
            CacheOptions,
            cancellationToken: ct) ?? [];
    }

    public ValueTask InvalidateAsync(string userId, CancellationToken ct = default) =>
        cache.RemoveAsync(Key(userId), ct);
}
