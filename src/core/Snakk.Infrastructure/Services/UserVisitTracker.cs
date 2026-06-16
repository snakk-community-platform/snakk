namespace Snakk.Infrastructure.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;

// Singleton: HybridCache is singleton; DB access uses a short-lived scope per cache miss.
public class UserVisitTracker(HybridCache hybridCache, IServiceScopeFactory scopeFactory) : IUserVisitTracker
{
    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(30)
    };

    public async Task TrackVisitAsync(string userId, CancellationToken ct = default)
    {
        await hybridCache.GetOrCreateAsync(
            $"session-active:{userId}",
            async token =>
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<SnakkDbContext>();
                await UpdateVisitTimestampsAsync(db, userId, hybridCache, token);
                return true;
            },
            CacheOptions,
            cancellationToken: ct);
    }

    public async Task ForceNewVisitAsync(string userId, CancellationToken ct = default)
    {
        // Invalidate session guard so next TrackVisitAsync triggers a fresh boundary check
        await hybridCache.RemoveAsync($"session-active:{userId}", ct);

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SnakkDbContext>();
        var now = DateTime.UtcNow;
        await db.Users
            .Where(u => u.PublicId == userId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.LastVisitAt, now)
                .SetProperty(u => u.CurrentVisitActiveAt, now), ct);

        await hybridCache.RemoveAsync($"last-visit-at:{userId}", ct);
    }

    private static async Task UpdateVisitTimestampsAsync(SnakkDbContext db, string userId, HybridCache cache, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var user = await db.Users
            .Where(u => u.PublicId == userId)
            .Select(u => new { u.CurrentVisitActiveAt, u.LastVisitAt })
            .FirstOrDefaultAsync(ct);

        if (user == null) return;

        var isNewVisit = !user.CurrentVisitActiveAt.HasValue
            || (now - user.CurrentVisitActiveAt.Value).TotalHours >= 4;

        if (isNewVisit)
        {
            DateTime? newLastVisitAt;

            if (user.CurrentVisitActiveAt.HasValue)
            {
                // Normal case: seal off the previous visit at its last heartbeat
                newLastVisitAt = user.CurrentVisitActiveAt.Value;
            }
            else
            {
                // First visit ever: seed LastVisitAt from the most recent discussion read
                // so existing users don't get flooded with all-time unread counts
                newLastVisitAt = await db.DiscussionReadStates
                    .Where(rs => rs.UserId == userId)
                    .MaxAsync(rs => (DateTime?)rs.LastReadAt, ct);
            }

            await db.Users
                .Where(u => u.PublicId == userId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(u => u.LastVisitAt, newLastVisitAt)
                    .SetProperty(u => u.CurrentVisitActiveAt, now), ct);

            await cache.RemoveAsync($"last-visit-at:{userId}", ct);
        }
        else
        {
            // Still within the same visit: heartbeat only
            await db.Users
                .Where(u => u.PublicId == userId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(u => u.CurrentVisitActiveAt, now), ct);
        }
    }
}
