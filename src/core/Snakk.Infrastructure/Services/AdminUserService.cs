using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Snakk.Application.DTOs.Admin;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;

namespace Snakk.Infrastructure.Services;

public class AdminUserService(
    SnakkDbContext context,
    IDbContextFactory<SnakkDbContext> dbFactory,
    HybridCache cache,
    ILogger<AdminUserService> logger) : IAdminUserService
{
    private static readonly HybridCacheEntryOptions CacheOptions = new() { Expiration = TimeSpan.FromMinutes(5) };

    private async Task<T> ReadAsync<T>(Func<SnakkDbContext, Task<T>> query)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await query(db);
    }

    public async Task<AdminUserDto?> GetUserByIdAsync(string userId, CancellationToken ct = default)
    {
        // Aggregate: TTL-only, no write-path invalidation. Display name/email are mutated
        // by user self-service flows that live outside this service (profile update,
        // email change) — a 5-minute-stale admin detail view is acceptable.
        var cacheKey = $"admin_user_{userId}";

        return await cache.GetOrCreateAsync(
            cacheKey,
            async cancel => await context.Users
                .Where(u => u.PublicId == userId)
                .Select(u => new AdminUserDto
                {
                    PublicId = u.PublicId,
                    DisplayName = u.DisplayName ?? "",
                    Email = u.Email!,
                    CreatedAt = u.CreatedAt
                })
                .FirstOrDefaultAsync(cancel),
            CacheOptions,
            cancellationToken: ct);
    }

    public async Task<bool> UserExistsAsync(string userId, CancellationToken ct = default) =>
        await context.Users.AnyAsync(u => u.PublicId == userId, ct);

    public async Task<PaginatedResponse<AdminBanDto>> GetActiveBansAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var offset = (page - 1) * pageSize;
        var now = DateTime.UtcNow;

        var countTask = ReadAsync(db => db.UserBans
            .Where(b => b.UnbannedAt == null && (b.ExpiresAt == null || b.ExpiresAt > now))
            .CountAsync(ct));

        var listTask = ReadAsync(db => db.UserBans
            .Where(b => b.UnbannedAt == null && (b.ExpiresAt == null || b.ExpiresAt > now))
            .OrderByDescending(b => b.BannedAt)
            .Skip(offset).Take(pageSize)
            .Select(b => new AdminBanDto
            {
                UserId = b.User.PublicId,
                UserDisplayName = b.User.DisplayName ?? "",
                Reason = b.Reason ?? "",
                BannedBy = b.BannedByUser!.DisplayName ?? "",
                BannedAt = b.BannedAt,
                ExpiresAt = b.ExpiresAt
            })
            .ToListAsync(ct));

        await Task.WhenAll(countTask, listTask);

        logger.LogInformation("Retrieved {Count} active bans (page {Page})", listTask.Result.Count, page);

        return new PaginatedResponse<AdminBanDto>
        {
            Items = listTask.Result,
            Total = countTask.Result,
            Page = page,
            PageSize = pageSize
        };
    }
}
