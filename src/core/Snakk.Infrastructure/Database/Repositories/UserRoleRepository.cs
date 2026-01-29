namespace Snakk.Infrastructure.Database.Repositories;

using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;

public class UserRoleRepository(SnakkDbContext context)
    : GenericDatabaseRepository<UserRoleDatabaseEntity>(context), IUserRoleRepository
{
    public async Task<UserRoleDatabaseEntity?> GetByPublicIdAsync(string publicId)
    {
        return await _dbSet
            .Include(ur => ur.User)
            .Include(ur => ur.Community)
            .Include(ur => ur.Hub)
            .Include(ur => ur.Space)
            .Include(ur => ur.AssignedByUser)
            .FirstOrDefaultAsync(ur => ur.PublicId == publicId);
    }

    public async Task<IEnumerable<UserRoleDatabaseEntity>> GetActiveRolesForUserAsync(int userId)
    {
        return await _dbSet
            .AsNoTracking()
            .Include(ur => ur.Community)
            .Include(ur => ur.Hub)
            .Include(ur => ur.Space)
            .Where(ur => ur.UserId == userId && ur.RevokedAt == null)
            .ToListAsync();
    }

    public async Task<IEnumerable<UserRoleDatabaseEntity>> GetActiveRolesForCommunityAsync(int communityId)
    {
        return await _dbSet
            .AsNoTracking()
            .Include(ur => ur.User)
            .Include(ur => ur.AssignedByUser)
            .Where(ur => ur.CommunityId == communityId && ur.RevokedAt == null)
            .OrderBy(ur => ur.Role)
            .ThenBy(ur => ur.AssignedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<UserRoleDatabaseEntity>> GetActiveRolesForHubAsync(int hubId)
    {
        return await _dbSet
            .AsNoTracking()
            .Include(ur => ur.User)
            .Include(ur => ur.AssignedByUser)
            .Where(ur => ur.HubId == hubId && ur.RevokedAt == null)
            .OrderBy(ur => ur.Role)
            .ThenBy(ur => ur.AssignedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<UserRoleDatabaseEntity>> GetActiveRolesForSpaceAsync(int spaceId)
    {
        return await _dbSet
            .AsNoTracking()
            .Include(ur => ur.User)
            .Include(ur => ur.AssignedByUser)
            .Where(ur => ur.SpaceId == spaceId && ur.RevokedAt == null)
            .OrderBy(ur => ur.Role)
            .ThenBy(ur => ur.AssignedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<UserRoleDatabaseEntity>> GetGlobalAdminsAsync()
    {
        return await _dbSet
            .AsNoTracking()
            .Include(ur => ur.User)
            .Include(ur => ur.AssignedByUser)
            .Where(ur => ur.Role == "GlobalAdmin" && ur.RevokedAt == null)
            .OrderBy(ur => ur.AssignedAt)
            .ToListAsync();
    }

    public async Task<bool> HasRoleAtOrAboveAsync(int userId, string roleType, int? communityId = null, int? hubId = null, int? spaceId = null)
    {
        // Check for exact role at the specified scope
        return await _dbSet
            .AsNoTracking()
            .AnyAsync(ur => 
                ur.UserId == userId && 
                ur.Role == roleType && 
                ur.RevokedAt == null &&
                (ur.CommunityId == communityId || communityId == null) &&
                (ur.HubId == hubId || hubId == null) &&
                (ur.SpaceId == spaceId || spaceId == null));
    }

    public async Task<bool> CanModerateAsync(int userId, int? communityId = null, int? hubId = null, int? spaceId = null)
    {
        // User can moderate if they have any of these roles at or above the scope:
        // - GlobalAdmin (can moderate anywhere)
        // - CommunityAdmin at the community level
        // - CommunityMod at the community level
        // - HubMod at the hub level (if checking hub or space)
        // - SpaceMod at the space level (if checking space)
        
        var activeRoles = await GetActiveRolesForUserAsync(userId);
        
        foreach (var role in activeRoles)
        {
            // GlobalAdmin can moderate anywhere
            if (role.Role == "GlobalAdmin")
                return true;
            
            // Check community-level roles
            if (communityId.HasValue && role.CommunityId == communityId)
            {
                if (role.Role == "CommunityAdmin" || role.Role == "CommunityMod")
                    return true;
            }
            
            // Check hub-level roles (need to check if hub belongs to community)
            if (hubId.HasValue && role.HubId == hubId && role.Role == "HubMod")
                return true;
            
            // Hub mods can moderate spaces within their hub - need to check this via the hub
            if (spaceId.HasValue && role.HubId.HasValue && role.Role == "HubMod")
            {
                var space = await _context.Spaces.AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Id == spaceId && s.HubId == role.HubId);
                if (space != null)
                    return true;
            }
            
            // Check space-level roles
            if (spaceId.HasValue && role.SpaceId == spaceId && role.Role == "SpaceMod")
                return true;
        }
        
        return false;
    }

    public async Task<bool> CanAdministerAsync(int userId, int? communityId = null, int? hubId = null, int? spaceId = null)
    {
        // Only admin roles can administer:
        // - GlobalAdmin (can administer anywhere)
        // - CommunityAdmin at the community level
        
        var activeRoles = await GetActiveRolesForUserAsync(userId);
        
        foreach (var role in activeRoles)
        {
            // GlobalAdmin can administer anywhere
            if (role.Role == "GlobalAdmin")
                return true;
            
            // CommunityAdmin can administer their community
            if (communityId.HasValue && role.CommunityId == communityId && role.Role == "CommunityAdmin")
                return true;
            
            // If checking a hub, need to find its community
            if (hubId.HasValue && role.Role == "CommunityAdmin" && role.CommunityId.HasValue)
            {
                var hub = await _context.Hubs.AsNoTracking()
                    .FirstOrDefaultAsync(h => h.Id == hubId && h.CommunityId == role.CommunityId);
                if (hub != null)
                    return true;
            }
            
            // If checking a space, need to find its community via hub
            if (spaceId.HasValue && role.Role == "CommunityAdmin" && role.CommunityId.HasValue)
            {
                var space = await _context.Spaces.AsNoTracking()
                    .Include(s => s.Hub)
                    .FirstOrDefaultAsync(s => s.Id == spaceId && s.Hub.CommunityId == role.CommunityId);
                if (space != null)
                    return true;
            }
        }
        
        return false;
    }
}
