using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Snakk.Application.DTOs.Admin;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;

namespace Snakk.Infrastructure.Services;

public class AdminUserService(
    SnakkDbContext context,
    IMemoryCache cache,
    ILogger<AdminUserService> logger) : IAdminUserService
{
    public async Task<AdminUserDto?> GetUserByIdAsync(string userId)
    {
        // Try cache first
        var cacheKey = $"admin_user_{userId}";

        if (cache.TryGetValue<AdminUserDto>(cacheKey, out var cachedUser))
            return cachedUser;

        var user = await context.Users
            .Where(u => u.PublicId == userId)
            .Select(u => new AdminUserDto
            {
                PublicId = u.PublicId,
                DisplayName = u.DisplayName,
                Email = u.Email!,
                CreatedAt = u.CreatedAt
            })
            .FirstOrDefaultAsync();

        if (user is not null)
        {
            // Cache for 5 minutes
            cache.Set(cacheKey, user, TimeSpan.FromMinutes(5));
        }

        return user;
    }

    public async Task<bool> UserExistsAsync(string userId) =>
        await context.Users.AnyAsync(u => u.PublicId == userId);

    public async Task<PaginatedResponse<AdminBanDto>> GetActiveBansAsync(int page, int pageSize)
    {
        var offset = (page - 1) * pageSize;
        var now = DateTime.UtcNow;

        var query = context.UserBans
            .Where(b =>
                b.UnbannedAt == null
                && (b.ExpiresAt == null || b.ExpiresAt > now))
            .OrderByDescending(b => b.BannedAt);

        var total = await query.CountAsync();

        var bans = await query
            .Skip(offset)
            .Take(pageSize)
            .Select(b => new AdminBanDto
            {
                UserId = b.User.PublicId,
                UserDisplayName = b.User.DisplayName,
                Reason = b.Reason,
                BannedBy = b.BannedByUser.DisplayName,
                BannedAt = b.BannedAt,
                ExpiresAt = b.ExpiresAt
            })
            .ToListAsync();

        logger.LogInformation("Retrieved {Count} active bans (page {Page})", bans.Count, page);

        return new PaginatedResponse<AdminBanDto>
        {
            Items = bans,
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }
}
