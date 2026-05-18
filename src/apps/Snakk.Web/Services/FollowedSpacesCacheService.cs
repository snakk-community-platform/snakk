namespace Snakk.Web.Services;

using Microsoft.Extensions.Caching.Memory;

public interface IFollowedSpacesCacheService
{
    Task<List<string>> GetAsync(string userId, Func<CancellationToken, Task<List<string>>> factory, CancellationToken ct = default);
    void Invalidate(string userId);
}

public class FollowedSpacesCacheService(IMemoryCache cache) : IFollowedSpacesCacheService
{
    private static string Key(string userId) => $"followed-spaces:{userId}";

    public async Task<List<string>> GetAsync(string userId, Func<CancellationToken, Task<List<string>>> factory, CancellationToken ct = default)
    {
        return await cache.GetOrCreateAsync(Key(userId), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2);
            return await factory(ct);
        }) ?? [];
    }

    public void Invalidate(string userId) => cache.Remove(Key(userId));
}
