namespace Snakk.Infrastructure.Database.Repositories;

using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Shared.Enums;

public class UserRoleRepository(SnakkDbContext context)
    : GenericDatabaseRepository<UserRoleDatabaseEntity>(context), IUserRoleRepository
{
    public async Task<UserRoleDatabaseEntity?> GetByPublicIdAsync(string publicId) =>
        await _dbSet.FirstOrDefaultAsync(ur => ur.PublicId == publicId);

    public async Task<IEnumerable<UserRoleDatabaseEntity>> GetActiveRolesForUserAsync(int userId) => await _dbSet
        .Where(ur => ur.UserId == userId && ur.RevokedAt == null)
        .ToListAsync();

    public async Task<IEnumerable<UserRoleDatabaseEntity>> GetActiveRolesForCommunityAsync(int communityId) => await _dbSet
        .Where(ur => ur.CommunityId == communityId && ur.RevokedAt == null)
        .OrderBy(ur => ur.RoleId)
        .ThenBy(ur => ur.AssignedAt)
        .ToListAsync();

    public async Task<IEnumerable<UserRoleDatabaseEntity>> GetActiveRolesForHubAsync(int hubId) => await _dbSet
        .Where(ur => ur.HubId == hubId && ur.RevokedAt == null)
        .OrderBy(ur => ur.RoleId)
        .ThenBy(ur => ur.AssignedAt)
        .ToListAsync();

    public async Task<IEnumerable<UserRoleDatabaseEntity>> GetActiveRolesForSpaceAsync(int spaceId) => await _dbSet
        .Where(ur => ur.SpaceId == spaceId && ur.RevokedAt == null)
        .OrderBy(ur => ur.RoleId)
        .ThenBy(ur => ur.AssignedAt)
        .ToListAsync();

    public async Task<IEnumerable<UserRoleDatabaseEntity>> GetGlobalAdminsAsync() => await _dbSet
        .Where(ur =>
            ur.RoleId == (int)UserRoleTypeEnum.GlobalAdmin
            && ur.RevokedAt == null)
        .OrderBy(ur => ur.AssignedAt)
        .ToListAsync();

    public async Task<bool> HasRoleAtOrAboveAsync(
        int userId,
        string roleType,
        int? communityId = null,
        int? hubId = null,
        int? spaceId = null)
    {
        // Check for exact role at the specified scope
        if (!Enum.TryParse<UserRoleTypeEnum>(roleType, out var roleEnum))
            return false;

        var roleId = (int)roleEnum;

        return await _dbSet.AnyAsync(ur =>
            ur.UserId == userId
            && ur.RoleId == roleId
            && ur.RevokedAt == null
            && (ur.CommunityId == communityId || communityId == null)
            && (ur.HubId == hubId || hubId == null)
            && (ur.SpaceId == spaceId || spaceId == null));
    }

    public async Task<bool> CanModerateAsync(
        int userId,
        int? communityId = null,
        int? hubId = null,
        int? spaceId = null)
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
            if (role.RoleId == (int)UserRoleTypeEnum.GlobalAdmin)
                return true;

            // Check community-level roles
            if (communityId.HasValue && role.CommunityId == communityId)
            {
                if (role.RoleId == (int)UserRoleTypeEnum.CommunityAdmin
                    || role.RoleId == (int)UserRoleTypeEnum.CommunityMod)
                    return true;
            }

            // Check hub-level roles (need to check if hub belongs to community)
            if (hubId.HasValue
                && role.HubId == hubId
                && role.RoleId == (int)UserRoleTypeEnum.HubMod)
                return true;

            // Hub mods can moderate spaces within their hub - need to check this via the hub
            if (spaceId.HasValue
                && role.HubId.HasValue
                && role.RoleId == (int)UserRoleTypeEnum.HubMod)
            {
                var space = await _context.Spaces
                    .FirstOrDefaultAsync(s =>
                        s.Id == spaceId
                        && s.HubId == role.HubId);

                if (space is not null)
                    return true;
            }

            // Check space-level roles
            if (spaceId.HasValue
                && role.SpaceId == spaceId
                && role.RoleId == (int)UserRoleTypeEnum.SpaceMod)
                return true;
        }

        return false;
    }

    public async Task<bool> CanAdministerAsync(
        int userId,
        int? communityId = null,
        int? hubId = null,
        int? spaceId = null)
    {
        // Only admin roles can administer:
        // - GlobalAdmin (can administer anywhere)
        // - CommunityAdmin at the community level

        var activeRoles = await GetActiveRolesForUserAsync(userId);

        foreach (var role in activeRoles)
        {
            // GlobalAdmin can administer anywhere
            if (role.RoleId == (int)UserRoleTypeEnum.GlobalAdmin)
                return true;

            // CommunityAdmin can administer their community
            if (communityId.HasValue
                && role.CommunityId == communityId
                && role.RoleId == (int)UserRoleTypeEnum.CommunityAdmin)
                return true;

            // If checking a hub, need to find its community
            if (hubId.HasValue
                && role.RoleId == (int)UserRoleTypeEnum.CommunityAdmin
                && role.CommunityId.HasValue)
            {
                var hub = await _context.Hubs
                    .FirstOrDefaultAsync(h =>
                        h.Id == hubId
                        && h.CommunityId == role.CommunityId);

                if (hub is not null)
                    return true;
            }

            // If checking a space, need to find its community via hub
            if (spaceId.HasValue
                && role.RoleId == (int)UserRoleTypeEnum.CommunityAdmin
                && role.CommunityId.HasValue)
            {
                var belongsToCommunity = await _context.Spaces
                    .AnyAsync(s =>
                        s.Id == spaceId
                        && s.Hub.CommunityId == role.CommunityId);

                if (belongsToCommunity)
                    return true;
            }
        }

        return false;
    }
}
