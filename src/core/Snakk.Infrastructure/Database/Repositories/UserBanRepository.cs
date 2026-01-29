namespace Snakk.Infrastructure.Database.Repositories;

using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;

public class UserBanRepository(SnakkDbContext context)
    : GenericDatabaseRepository<UserBanDatabaseEntity>(context), IUserBanRepository
{
    public async Task<UserBanDatabaseEntity?> GetByPublicIdAsync(string publicId)
    {
        return await _dbSet
            .Include(ub => ub.User)
            .Include(ub => ub.Community)
            .Include(ub => ub.Hub)
            .Include(ub => ub.Space)
            .Include(ub => ub.BannedByUser)
            .Include(ub => ub.UnbannedByUser)
            .FirstOrDefaultAsync(ub => ub.PublicId == publicId);
    }

    public async Task<IEnumerable<UserBanDatabaseEntity>> GetActiveBansForUserAsync(int userId)
    {
        var now = DateTime.UtcNow;
        return await _dbSet
            .AsNoTracking()
            .Include(ub => ub.Community)
            .Include(ub => ub.Hub)
            .Include(ub => ub.Space)
            .Include(ub => ub.BannedByUser)
            .Where(ub => 
                ub.UserId == userId && 
                ub.UnbannedAt == null &&
                (ub.ExpiresAt == null || ub.ExpiresAt > now))
            .ToListAsync();
    }

    public async Task<UserBanDatabaseEntity?> GetActiveBanForScopeAsync(int userId, int? communityId = null, int? hubId = null, int? spaceId = null)
    {
        var now = DateTime.UtcNow;
        
        // Build query for active bans
        var query = _dbSet
            .AsNoTracking()
            .Where(ub => 
                ub.UserId == userId && 
                ub.UnbannedAt == null &&
                (ub.ExpiresAt == null || ub.ExpiresAt > now));
        
        // Check for platform-wide ban first (all scope fields null)
        var platformBan = await query
            .FirstOrDefaultAsync(ub => 
                ub.CommunityId == null && ub.HubId == null && ub.SpaceId == null);
        if (platformBan != null)
            return platformBan;
        
        // Check community-level ban if communityId provided
        if (communityId.HasValue)
        {
            var communityBan = await query
                .FirstOrDefaultAsync(ub => ub.CommunityId == communityId);
            if (communityBan != null)
                return communityBan;
        }
        
        // Check hub-level ban if hubId provided
        if (hubId.HasValue)
        {
            var hubBan = await query
                .FirstOrDefaultAsync(ub => ub.HubId == hubId);
            if (hubBan != null)
                return hubBan;
            
            // Also check if hub's community has a ban
            if (!communityId.HasValue)
            {
                var hub = await _context.Hubs.AsNoTracking()
                    .FirstOrDefaultAsync(h => h.Id == hubId);
                if (hub != null)
                {
                    var hubCommunityBan = await query
                        .FirstOrDefaultAsync(ub => ub.CommunityId == hub.CommunityId);
                    if (hubCommunityBan != null)
                        return hubCommunityBan;
                }
            }
        }
        
        // Check space-level ban if spaceId provided
        if (spaceId.HasValue)
        {
            var spaceBan = await query
                .FirstOrDefaultAsync(ub => ub.SpaceId == spaceId);
            if (spaceBan != null)
                return spaceBan;
            
            // Also check hub and community bans for the space
            if (!hubId.HasValue)
            {
                var space = await _context.Spaces.AsNoTracking()
                    .Include(s => s.Hub)
                    .FirstOrDefaultAsync(s => s.Id == spaceId);
                if (space != null)
                {
                    // Check hub-level ban
                    var spaceHubBan = await query
                        .FirstOrDefaultAsync(ub => ub.HubId == space.HubId);
                    if (spaceHubBan != null)
                        return spaceHubBan;
                    
                    // Check community-level ban
                    var spaceCommunityBan = await query
                        .FirstOrDefaultAsync(ub => ub.CommunityId == space.Hub.CommunityId);
                    if (spaceCommunityBan != null)
                        return spaceCommunityBan;
                }
            }
        }
        
        return null;
    }

    public async Task<bool> IsUserBannedAsync(int userId, int? communityId = null, int? hubId = null, int? spaceId = null)
    {
        var ban = await GetActiveBanForScopeAsync(userId, communityId, hubId, spaceId);
        return ban != null;
    }

    public async Task<IEnumerable<UserBanDatabaseEntity>> GetBansForCommunityAsync(int communityId)
    {
        return await _dbSet
            .AsNoTracking()
            .Include(ub => ub.User)
            .Include(ub => ub.BannedByUser)
            .Include(ub => ub.UnbannedByUser)
            .Where(ub => ub.CommunityId == communityId)
            .OrderByDescending(ub => ub.BannedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<UserBanDatabaseEntity>> GetBansForHubAsync(int hubId)
    {
        return await _dbSet
            .AsNoTracking()
            .Include(ub => ub.User)
            .Include(ub => ub.BannedByUser)
            .Include(ub => ub.UnbannedByUser)
            .Where(ub => ub.HubId == hubId)
            .OrderByDescending(ub => ub.BannedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<UserBanDatabaseEntity>> GetBansForSpaceAsync(int spaceId)
    {
        return await _dbSet
            .AsNoTracking()
            .Include(ub => ub.User)
            .Include(ub => ub.BannedByUser)
            .Include(ub => ub.UnbannedByUser)
            .Where(ub => ub.SpaceId == spaceId)
            .OrderByDescending(ub => ub.BannedAt)
            .ToListAsync();
    }
}
