namespace Snakk.Api.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;

// Singleton: HybridCache is singleton; DB access uses a short-lived scope per cache miss.
public class AuthVersionCache(HybridCache hybridCache, IServiceScopeFactory scopeFactory) : IAuthVersionCache
{
    private static readonly Random Jitter = new();

    private static HybridCacheEntryOptions MakeOptions() => new()
    {
        Expiration = TimeSpan.FromSeconds(300 + Jitter.Next(0, 30))
    };

    private static string Key(string userId) => $"authver:{userId}";

    public async Task<long?> GetVersionAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await hybridCache.GetOrCreateAsync(
            Key(userId),
            async token =>
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<SnakkDbContext>();
                return await db.Users
                    .Where(u => u.PublicId == userId && !u.IsDeleted)
                    .Select(u => (long?)u.AuthVersion)
                    .FirstOrDefaultAsync(token);
            },
            MakeOptions(),
            cancellationToken: cancellationToken);
    }

    public async Task InvalidateAsync(string userId, CancellationToken cancellationToken = default) =>
        await hybridCache.RemoveAsync(Key(userId), cancellationToken);

    public async Task SetAsync(string userId, long version, CancellationToken cancellationToken = default) =>
        await hybridCache.SetAsync(Key(userId), (long?)version, MakeOptions(), cancellationToken: cancellationToken);
}
