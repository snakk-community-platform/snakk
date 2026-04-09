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
        var activeRoles = await GetActiveRolesForUserAsync(userId);

        // Resolve the space's hub ID once (not per role)
        int? spaceHubId = null;
        if (spaceId.HasValue)
            spaceHubId = await _context.Spaces.Where(s => s.Id == spaceId).Select(s => (int?)s.HubId).FirstOrDefaultAsync();

        foreach (var role in activeRoles)
        {
            if (role.RoleId == (int)UserRoleTypeEnum.GlobalAdmin)
                return true;

            if (communityId.HasValue && role.CommunityId == communityId
                && (role.RoleId == (int)UserRoleTypeEnum.CommunityAdmin
                    || role.RoleId == (int)UserRoleTypeEnum.CommunityMod))
                return true;

            if (hubId.HasValue
                && role.HubId == hubId
                && role.RoleId == (int)UserRoleTypeEnum.HubMod)
                return true;

            // Hub mods can moderate spaces within their hub
            if (spaceId.HasValue
                && spaceHubId.HasValue
                && role.HubId == spaceHubId
                && role.RoleId == (int)UserRoleTypeEnum.HubMod)
                return true;

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
        var activeRoles = await GetActiveRolesForUserAsync(userId);

        // Resolve parent community IDs once (not per role)
        int? hubCommunityId = null;
        if (hubId.HasValue)
            hubCommunityId = await _context.Hubs.Where(h => h.Id == hubId).Select(h => (int?)h.CommunityId).FirstOrDefaultAsync();

        int? spaceCommunityId = null;
        if (spaceId.HasValue)
            spaceCommunityId = await _context.Spaces.Where(s => s.Id == spaceId).Select(s => (int?)s.Hub.CommunityId).FirstOrDefaultAsync();

        foreach (var role in activeRoles)
        {
            if (role.RoleId == (int)UserRoleTypeEnum.GlobalAdmin)
                return true;

            if (role.RoleId != (int)UserRoleTypeEnum.CommunityAdmin || !role.CommunityId.HasValue)
                continue;

            if (communityId.HasValue && role.CommunityId == communityId)
                return true;

            if (hubId.HasValue && hubCommunityId == role.CommunityId)
                return true;

            if (spaceId.HasValue && spaceCommunityId == role.CommunityId)
                return true;
        }

        return false;
    }
}
