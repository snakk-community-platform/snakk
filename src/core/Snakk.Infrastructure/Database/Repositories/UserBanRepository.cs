namespace Snakk.Infrastructure.Database.Repositories;

using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;

public class UserBanRepository(SnakkDbContext context)
    : GenericDatabaseRepository<UserBanDatabaseEntity>(context), IUserBanRepository
{
    public async Task<UserBanDatabaseEntity?> GetByPublicIdAsync(string publicId, CancellationToken ct = default) => await _dbSet
        .Include(ub => ub.User)
        .Include(ub => ub.Community)
        .Include(ub => ub.Hub)
        .Include(ub => ub.Space)
        .Include(ub => ub.BannedByUser)
        .Include(ub => ub.UnbannedByUser)
        .FirstOrDefaultAsync(ub => ub.PublicId == publicId, ct);

    public async Task<IEnumerable<UserBanDatabaseEntity>> GetActiveBansForUserAsync(int userId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        return await _dbSet
            .Include(ub => ub.Community)
            .Include(ub => ub.Hub)
            .Include(ub => ub.Space)
            .Include(ub => ub.BannedByUser)
            .Where(ub =>
                ub.UserId == userId
                && ub.UnbannedAt == null
                && (ub.ExpiresAt == null || ub.ExpiresAt > now))
            .ToListAsync(ct);
    }

    public async Task<UserBanDatabaseEntity?> GetActiveBanForScopeAsync(
        int userId,
        int? communityId = null,
        int? hubId = null,
        int? spaceId = null,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        // Resolve the full hierarchy ids in one query when a space is given
        // but hub/community are not yet known.
        if (spaceId.HasValue && !hubId.HasValue)
        {
            var hierarchy = await _context.Spaces
                .Where(s => s.Id == spaceId)
                .Select(s => new { s.HubId, CommunityId = s.Hub.CommunityId })
                .FirstOrDefaultAsync(ct);

            if (hierarchy is not null)
            {
                hubId ??= hierarchy.HubId;
                communityId ??= hierarchy.CommunityId;
            }
        }
        else if (hubId.HasValue && !communityId.HasValue)
        {
            communityId = await _context.Hubs
                .Where(h => h.Id == hubId)
                .Select(h => (int?)h.CommunityId)
                .FirstOrDefaultAsync(ct);
        }

        // Single query: fetch all active bans that match any of the relevant scopes,
        // ordered most-specific-first (platform > community > hub > space) so the
        // caller gets the highest-priority ban — matching the original precedence:
        //   platform-wide (0) > community (1) > hub (2) > space (3)
        return await _dbSet
            .Where(ub =>
                ub.UserId == userId
                && ub.UnbannedAt == null
                && (ub.ExpiresAt == null || ub.ExpiresAt > now)
                && (
                    (ub.CommunityId == null && ub.HubId == null && ub.SpaceId == null) // platform-wide
                    || (communityId.HasValue && ub.CommunityId == communityId && ub.HubId == null && ub.SpaceId == null)
                    || (hubId.HasValue && ub.HubId == hubId && ub.SpaceId == null)
                    || (spaceId.HasValue && ub.SpaceId == spaceId)
                ))
            .OrderBy(ub =>
                ub.CommunityId == null && ub.HubId == null && ub.SpaceId == null ? 0
                : ub.CommunityId != null ? 1
                : ub.HubId != null ? 2
                : 3)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<bool> IsUserBannedAsync(
        int userId,
        int? communityId = null,
        int? hubId = null,
        int? spaceId = null,
        CancellationToken ct = default)
    {
        var ban = await GetActiveBanForScopeAsync(userId, communityId, hubId, spaceId, ct);
        return ban is not null;
    }

    public async Task<IEnumerable<UserBanDatabaseEntity>> GetBansForCommunityAsync(int communityId, CancellationToken ct = default) => await _dbSet
        .Include(ub => ub.User)
        .Include(ub => ub.BannedByUser)
        .Include(ub => ub.UnbannedByUser)
        .Where(ub => ub.CommunityId == communityId)
        .OrderByDescending(ub => ub.BannedAt)
        .ToListAsync(ct);

    public async Task<IEnumerable<UserBanDatabaseEntity>> GetBansForHubAsync(int hubId, CancellationToken ct = default) => await _dbSet
        .Include(ub => ub.User)
        .Include(ub => ub.BannedByUser)
        .Include(ub => ub.UnbannedByUser)
        .Where(ub => ub.HubId == hubId)
        .OrderByDescending(ub => ub.BannedAt)
        .ToListAsync(ct);

    public async Task<IEnumerable<UserBanDatabaseEntity>> GetBansForSpaceAsync(int spaceId, CancellationToken ct = default) => await _dbSet
        .Include(ub => ub.User)
        .Include(ub => ub.BannedByUser)
        .Include(ub => ub.UnbannedByUser)
        .Where(ub => ub.SpaceId == spaceId)
        .OrderByDescending(ub => ub.BannedAt)
        .ToListAsync(ct);
}
