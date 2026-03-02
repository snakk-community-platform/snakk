using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Snakk.Application.DTOs.Security;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Shared;
using Snakk.Shared.Enums;

namespace Snakk.Infrastructure.Services;

public class PermissionService : IPermissionService
{
    private readonly SnakkDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly ILogger<PermissionService> _logger;
    private readonly ISecurityService _securityService;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    // Compiled queries for hot-path permission lookups
    private static readonly Func<SnakkDbContext, string, Task<int?>> _getUserIdByPublicId
        = EF.CompileAsyncQuery(
            (SnakkDbContext ctx, string publicId) => ctx.Users
                .Where(u => u.PublicId == publicId)
                .Select(u => (int?)u.Id)
                .FirstOrDefault());

    private static readonly Func<SnakkDbContext, string, Task<int?>> _getCommunityIdByPublicId
        = EF.CompileAsyncQuery(
            (SnakkDbContext ctx, string publicId) => ctx.Communities
                .Where(c => c.PublicId == publicId)
                .Select(c => (int?)c.Id)
                .FirstOrDefault());

    private static readonly Func<SnakkDbContext, string, Task<HubScope?>> _getHubScopeByPublicId
        = EF.CompileAsyncQuery(
            (SnakkDbContext ctx, string publicId) => ctx.Hubs
                .Where(h => h.PublicId == publicId)
                .Select(h => new HubScope(h.Id, h.CommunityId))
                .FirstOrDefault());

    private static readonly Func<SnakkDbContext, string, Task<SpaceScope?>> _getSpaceScopeByPublicId
        = EF.CompileAsyncQuery(
            (SnakkDbContext ctx, string publicId) => ctx.Spaces
                .Where(s => s.PublicId == publicId)
                .Select(s => new SpaceScope(s.Id, s.HubId, s.Hub.CommunityId))
                .FirstOrDefault());

    private static readonly Func<SnakkDbContext, string, Task<ScopeIds?>> _getDiscussionScopeByPublicId
        = EF.CompileAsyncQuery(
            (SnakkDbContext ctx, string publicId) => ctx.Discussions
                .Where(d => d.PublicId == publicId)
                .Select(d => new ScopeIds(d.SpaceId, d.Space.HubId, d.Space.Hub.CommunityId))
                .FirstOrDefault());

    private static readonly Func<SnakkDbContext, string, Task<ScopeIds?>> _getPostScopeByPublicId
        = EF.CompileAsyncQuery(
            (SnakkDbContext ctx, string publicId) => ctx.Posts
                .Where(p => p.PublicId == publicId)
                .Select(p => new ScopeIds(p.Discussion.SpaceId, p.Discussion.Space.HubId, p.Discussion.Space.Hub.CommunityId))
                .FirstOrDefault());

    public PermissionService(
        SnakkDbContext context,
        IMemoryCache cache,
        ILogger<PermissionService> logger,
        ISecurityService securityService)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
        _securityService = securityService;
    }

    public async Task<bool> UserHasPermissionAsync(string userId, string permissionName, string? scope = null, string? scopePublicId = null)
    {
        // Get user's database ID from PublicId
        var userDbId = await _getUserIdByPublicId(_context, userId);
        if (userDbId == null)
            return false;

        // Get all active roles for the user (including temporary elevations)
        var userRoles = await GetUserRolesWithScopeAsync(userDbId.Value);

        // HIERARCHY: GlobalAdmin has access to EVERYTHING
        if (userRoles.Any(r => r.RoleType == "GlobalAdmin"))
        {
            _logger.LogDebug("User {UserId} has GlobalAdmin role - granting access to {Permission}", userId, permissionName);
            return true;
        }

        // If scope is specified, check hierarchical access
        if (!string.IsNullOrEmpty(scope) && !string.IsNullOrEmpty(scopePublicId))
        {
            var hasHierarchicalAccess = await CheckHierarchicalAccessAsync(userRoles, scope, scopePublicId);
            if (hasHierarchicalAccess)
            {
                _logger.LogDebug("User {UserId} has hierarchical access to {Scope}:{ScopePublicId}", userId, scope, scopePublicId);
                return true;
            }
        }

        // Finally, check explicit permissions for the user's roles
        var cacheKey = $"user_permissions_{userId}";
        var userPermissions = await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            return await GetUserPermissionsInternalAsync(userId);
        });

        if (userPermissions == null)
            return false;

        return userPermissions.Any(p => p.Name == permissionName);
    }

    private async Task<List<UserRoleScope>> GetUserRolesWithScopeAsync(int userId)
    {
        var roles = await _context.UserRoles
            .Where(ur => ur.UserId == userId && ur.RevokedAt == null)
            .Select(ur => new UserRoleScope
            {
                RoleType = ((UserRoleTypeEnum)ur.RoleId).ToString(),
                CommunityId = ur.CommunityId,
                HubId = ur.HubId,
                SpaceId = ur.SpaceId
            })
            .ToListAsync();

        // Also include active temporary role elevations
        var now = DateTime.UtcNow;
        var tempRoles = await _context.TemporaryRoleElevations
            .Where(e => e.UserId == userId && e.RevokedAt == null && e.ExpiresAt > now)
            .Select(e => new UserRoleScope
            {
                RoleType = e.RoleType,
                CommunityId = e.Scope == "Community" ? e.ScopeId : (int?)null,
                HubId = e.Scope == "Hub" ? e.ScopeId : (int?)null,
                SpaceId = e.Scope == "Space" ? e.ScopeId : (int?)null
            })
            .ToListAsync();

        roles.AddRange(tempRoles);
        return roles;
    }

    private async Task<bool> CheckHierarchicalAccessAsync(List<UserRoleScope> userRoles, string scope, string scopePublicId)
    {
        switch (scope.ToLower())
        {
            case "community":
                // Resolve community publicId to internal ID
                var communityId = await _getCommunityIdByPublicId(_context, scopePublicId);
                if (communityId == null)
                    return false;

                // Check if user is CommunityAdmin or CommunityMod for this community
                return userRoles.Any(r =>
                    (r.RoleType == "CommunityAdmin" || r.RoleType == "CommunityMod") &&
                    r.CommunityId == communityId.Value);

            case "hub":
                // Resolve hub publicId to internal ID + parent community
                var hub = await _getHubScopeByPublicId(_context, scopePublicId);
                if (hub == null)
                    return false;

                // Check if user is:
                // 1. HubMod for this hub, OR
                // 2. CommunityAdmin/CommunityMod for the parent community
                return userRoles.Any(r =>
                    (r.RoleType == "HubMod" && r.HubId == hub.Id) ||
                    ((r.RoleType == "CommunityAdmin" || r.RoleType == "CommunityMod") && r.CommunityId == hub.CommunityId));

            case "space":
                // Resolve space publicId to internal ID + parent hub/community
                var space = await _getSpaceScopeByPublicId(_context, scopePublicId);
                if (space == null)
                    return false;

                // Check if user is:
                // 1. SpaceMod for this space, OR
                // 2. HubMod for the parent hub, OR
                // 3. CommunityAdmin/CommunityMod for the parent community
                return userRoles.Any(r =>
                    (r.RoleType == "SpaceMod" && r.SpaceId == space.Id) ||
                    (r.RoleType == "HubMod" && r.HubId == space.HubId) ||
                    ((r.RoleType == "CommunityAdmin" || r.RoleType == "CommunityMod") && r.CommunityId == space.CommunityId));

            case "discussion":
                // Resolve discussion publicId to internal IDs for parent space/hub/community
                var discussion = await _getDiscussionScopeByPublicId(_context, scopePublicId);
                if (discussion == null)
                    return false;

                return userRoles.Any(r =>
                    (r.RoleType == "SpaceMod" && r.SpaceId == discussion.SpaceId) ||
                    (r.RoleType == "HubMod" && r.HubId == discussion.HubId) ||
                    ((r.RoleType == "CommunityAdmin" || r.RoleType == "CommunityMod") && r.CommunityId == discussion.CommunityId));

            case "post":
                // Resolve post publicId to internal IDs for parent discussion/space/hub/community
                var post = await _getPostScopeByPublicId(_context, scopePublicId);
                if (post == null)
                    return false;

                return userRoles.Any(r =>
                    (r.RoleType == "SpaceMod" && r.SpaceId == post.SpaceId) ||
                    (r.RoleType == "HubMod" && r.HubId == post.HubId) ||
                    ((r.RoleType == "CommunityAdmin" || r.RoleType == "CommunityMod") && r.CommunityId == post.CommunityId));

            default:
                return false;
        }
    }

    private class UserRoleScope
    {
        public string RoleType { get; set; } = string.Empty;
        public int? CommunityId { get; set; }
        public int? HubId { get; set; }
        public int? SpaceId { get; set; }
    }

    // Projection records for compiled queries
    private record HubScope(int Id, int CommunityId);
    private record SpaceScope(int Id, int HubId, int CommunityId);
    private record ScopeIds(int SpaceId, int HubId, int CommunityId);

    public async Task<List<PermissionDto>> GetUserPermissionsAsync(string userId)
    {
        var cacheKey = $"user_permissions_{userId}";

        var permissions = await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            return await GetUserPermissionsInternalAsync(userId);
        });

        return permissions ?? new List<PermissionDto>();
    }

    private async Task<List<PermissionDto>> GetUserPermissionsInternalAsync(string userId)
    {
        // Get user's database ID from PublicId
        var userDbId = await _getUserIdByPublicId(_context, userId);
        if (userDbId == null)
            return new List<PermissionDto>();

        // Get all active roles for the user
        var userRoleIds = await _context.UserRoles
            .Where(ur => ur.UserId == userDbId.Value && ur.RevokedAt == null)
            .Select(ur => ur.Id)
            .ToListAsync();

        if (!userRoleIds.Any())
            return new List<PermissionDto>();

        // Get all permissions assigned to those roles
        var permissions = await _context.RolePermissions
            .Where(rp => userRoleIds.Contains(rp.RoleId))
            .Select(rp => rp.Permission)
            .Distinct()
            .Select(p => new PermissionDto
            {
                PublicId = p.PublicId,
                Name = p.Name,
                Category = p.Category,
                Description = p.Description,
                IsSystemPermission = p.IsSystemPermission,
                CreatedAt = p.CreatedAt
            })
            .ToListAsync();

        return permissions;
    }

    public async Task<List<PermissionDto>> GetRolePermissionsAsync(int roleId)
    {
        return await _context.RolePermissions
            .Where(rp => rp.RoleId == roleId)
            .Select(rp => new PermissionDto
            {
                PublicId = rp.Permission.PublicId,
                Name = rp.Permission.Name,
                Category = rp.Permission.Category,
                Description = rp.Permission.Description,
                IsSystemPermission = rp.Permission.IsSystemPermission,
                CreatedAt = rp.Permission.CreatedAt
            })
            .ToListAsync();
    }

    public async Task<List<PermissionDto>> GetAllPermissionsAsync()
    {
        var cacheKey = "all_permissions";

        var permissions = await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;

            return await _context.Permissions
                .OrderBy(p => p.Category)
                .ThenBy(p => p.Name)
                .Select(p => new PermissionDto
                {
                    PublicId = p.PublicId,
                    Name = p.Name,
                    Category = p.Category,
                    Description = p.Description,
                    IsSystemPermission = p.IsSystemPermission,
                    CreatedAt = p.CreatedAt
                })
                .ToListAsync();
        });

        return permissions ?? new List<PermissionDto>();
    }

    public async Task AssignPermissionToRoleAsync(int roleId, int permissionId, string adminUserId)
    {
        // Check if the assignment already exists
        var exists = await _context.RolePermissions
            .AnyAsync(rp => rp.RoleId == roleId && rp.PermissionId == permissionId);

        if (exists)
        {
            _logger.LogWarning("Permission {PermissionId} already assigned to role {RoleId}", permissionId, roleId);
            return;
        }

        // Get admin user database ID
        var adminDbId = await _getUserIdByPublicId(_context, adminUserId);

        var rolePermission = new RolePermissionDatabaseEntity
        {
            RoleId = roleId,
            PermissionId = permissionId,
            GrantedAt = DateTime.UtcNow,
            GrantedById = adminDbId
        };

        _context.RolePermissions.Add(rolePermission);
        await _context.SaveChangesAsync();

        // Invalidate cache for all users with this role
        await InvalidateRoleCacheAsync(roleId);

        // Log audit event
        var permission = await _context.Permissions.FindAsync(permissionId);
        var role = await _context.UserRoles.FirstOrDefaultAsync(r => r.Id == roleId);
        var roleName = role != null ? ((UserRoleTypeEnum)role.RoleId).ToString() : "Unknown";

        await _securityService.LogAuditAsync(
            adminUserId,
            "AssignPermission",
            $"Assigned permission '{permission?.Name}' to role '{roleName}'",
            "Permission",
            permissionId.ToString());

        _logger.LogInformation("Assigned permission {PermissionId} to role {RoleId} by admin {AdminUserId}",
            permissionId, roleId, adminUserId);
    }

    public async Task RevokePermissionFromRoleAsync(int roleId, int permissionId, string adminUserId)
    {
        var rolePermission = await _context.RolePermissions
            .FirstOrDefaultAsync(rp => rp.RoleId == roleId && rp.PermissionId == permissionId);

        if (rolePermission == null)
        {
            _logger.LogWarning("Permission {PermissionId} not found for role {RoleId}", permissionId, roleId);
            return;
        }

        _context.RolePermissions.Remove(rolePermission);
        await _context.SaveChangesAsync();

        // Invalidate cache for all users with this role
        await InvalidateRoleCacheAsync(roleId);

        // Log audit event
        var permission = await _context.Permissions.FindAsync(permissionId);
        var role = await _context.UserRoles.FirstOrDefaultAsync(r => r.Id == roleId);
        var roleName = role != null ? ((UserRoleTypeEnum)role.RoleId).ToString() : "Unknown";

        await _securityService.LogAuditAsync(
            adminUserId,
            "RevokePermission",
            $"Revoked permission '{permission?.Name}' from role '{roleName}'",
            "Permission",
            permissionId.ToString());

        _logger.LogInformation("Revoked permission {PermissionId} from role {RoleId} by admin {AdminUserId}",
            permissionId, roleId, adminUserId);
    }

    public async Task<TemporaryRoleElevationDto> GrantTemporaryRoleAsync(
        string userId,
        string roleType,
        string scope,
        int scopeId,
        DateTime expiresAt,
        string reason,
        string adminUserId)
    {
        // Get user and admin database IDs
        var user = await _context.Users
            .Where(u => u.PublicId == userId)
            .Select(u => new { u.Id, u.DisplayName })
            .FirstOrDefaultAsync();

        if (user == null)
            throw new InvalidOperationException($"User {userId} not found");

        var adminUser = await _context.Users
            .Where(a => a.PublicId == adminUserId)
            .Select(a => new { a.Id, a.Email })
            .FirstOrDefaultAsync();

        if (adminUser == null)
            throw new InvalidOperationException($"Admin user {adminUserId} not found");

        var elevation = new TemporaryRoleElevationDatabaseEntity
        {
            PublicId = Guid.NewGuid().ToString("N"),
            UserId = user.Id,
            RoleType = roleType,
            Scope = scope,
            ScopeId = scopeId,
            ExpiresAt = expiresAt,
            Reason = reason,
            GrantedById = adminUser.Id,
            CreatedAt = DateTime.UtcNow
        };

        _context.TemporaryRoleElevations.Add(elevation);
        await _context.SaveChangesAsync();

        // Invalidate user's permission cache
        _cache.Remove($"user_permissions_{userId}");

        // Log audit event
        await _securityService.LogAuditAsync(
            adminUserId,
            "GrantTemporaryRole",
            $"Granted temporary {roleType} role to user {user.DisplayName} in {scope}:{scopeId}, expires at {expiresAt:u}",
            "TemporaryRole",
            elevation.PublicId);

        _logger.LogInformation("Granted temporary role {RoleType} to user {UserId} by admin {AdminUserId}, expires at {ExpiresAt}",
            roleType, userId, adminUserId, expiresAt);

        return new TemporaryRoleElevationDto
        {
            PublicId = elevation.PublicId,
            UserId = user.Id,
            UserDisplayName = user.DisplayName,
            RoleType = roleType,
            Scope = scope,
            ScopeId = scopeId,
            ExpiresAt = expiresAt,
            Reason = reason,
            GrantedById = adminUser.Id,
            GrantedByEmail = adminUser.Email,
            CreatedAt = elevation.CreatedAt
        };
    }

    public async Task RevokeTemporaryRoleAsync(string elevationId, string reason, string adminUserId)
    {
        var elevation = await _context.TemporaryRoleElevations
            .AsTracking()
            .Include(e => e.User)
            .FirstOrDefaultAsync(e => e.PublicId == elevationId && e.RevokedAt == null);

        if (elevation == null)
        {
            _logger.LogWarning("Temporary role elevation {ElevationId} not found or already revoked", elevationId);
            return;
        }

        // Get admin user database ID
        var adminDbId = await _getUserIdByPublicId(_context, adminUserId);

        elevation.RevokedAt = DateTime.UtcNow;
        elevation.RevokedById = adminDbId;
        elevation.RevokedReason = reason;

        await _context.SaveChangesAsync();

        // Invalidate user's permission cache
        _cache.Remove($"user_permissions_{elevation.User.PublicId}");

        // Log audit event
        await _securityService.LogAuditAsync(
            adminUserId,
            "RevokeTemporaryRole",
            $"Revoked temporary role elevation for user {elevation.User.DisplayName}. Reason: {reason}",
            "TemporaryRole",
            elevationId);

        _logger.LogInformation("Revoked temporary role elevation {ElevationId} by admin {AdminUserId}",
            elevationId, adminUserId);
    }

    public async Task<List<TemporaryRoleElevationDto>> GetActiveTemporaryRolesAsync()
    {
        var now = DateTime.UtcNow;

        return await _context.TemporaryRoleElevations
            .Where(e => e.RevokedAt == null && e.ExpiresAt > now)
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => new TemporaryRoleElevationDto
            {
                PublicId = e.PublicId,
                UserId = e.UserId,
                UserDisplayName = e.User!.DisplayName,
                RoleType = e.RoleType,
                Scope = e.Scope,
                ScopeId = e.ScopeId,
                ExpiresAt = e.ExpiresAt,
                Reason = e.Reason,
                GrantedById = e.GrantedById,
                GrantedByEmail = e.GrantedBy!.Email,
                RevokedAt = e.RevokedAt,
                RevokedById = e.RevokedById,
                RevokedByEmail = e.RevokedBy != null ? e.RevokedBy.Email : null,
                RevokedReason = e.RevokedReason,
                CreatedAt = e.CreatedAt
            })
            .ToListAsync();
    }

    public async Task<List<TemporaryRoleElevationDto>> GetUserTemporaryRolesAsync(string userId)
    {
        var userDbId = await _getUserIdByPublicId(_context, userId);
        if (userDbId == null)
            return new List<TemporaryRoleElevationDto>();

        return await _context.TemporaryRoleElevations
            .Where(e => e.UserId == userDbId.Value)
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => new TemporaryRoleElevationDto
            {
                PublicId = e.PublicId,
                UserId = e.UserId,
                UserDisplayName = e.User!.DisplayName,
                RoleType = e.RoleType,
                Scope = e.Scope,
                ScopeId = e.ScopeId,
                ExpiresAt = e.ExpiresAt,
                Reason = e.Reason,
                GrantedById = e.GrantedById,
                GrantedByEmail = e.GrantedBy!.Email,
                RevokedAt = e.RevokedAt,
                RevokedById = e.RevokedById,
                RevokedByEmail = e.RevokedBy != null ? e.RevokedBy.Email : null,
                RevokedReason = e.RevokedReason,
                CreatedAt = e.CreatedAt
            })
            .ToListAsync();
    }

    private async Task InvalidateRoleCacheAsync(int roleId)
    {
        // Get all user IDs that have this role
        var userIds = await _context.UserRoles
            .Where(ur => ur.Id == roleId && ur.RevokedAt == null)
            .Select(ur => ur.User.PublicId)
            .ToListAsync();

        // Invalidate cache for each user
        foreach (var userId in userIds)
        {
            _cache.Remove($"user_permissions_{userId}");
        }
    }
}
