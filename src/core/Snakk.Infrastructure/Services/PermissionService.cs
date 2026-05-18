using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Snakk.Application.DTOs.Security;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Shared;
using Snakk.Shared.Enums;

namespace Snakk.Infrastructure.Services;

public class PermissionService(
    SnakkDbContext context,
    IDbContextFactory<SnakkDbContext> dbFactory,
    HybridCache cache,
    ILogger<PermissionService> logger,
    ISecurityService securityService) : IPermissionService
{
    private static readonly HybridCacheEntryOptions CacheOptions = new() { Expiration = TimeSpan.FromMinutes(5) };

    private async Task<T> ReadAsync<T>(Func<SnakkDbContext, Task<T>> query)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await query(db);
    }

    // Regular async queries — compiled queries are incompatible with multiple EF providers
    // (InMemory + SQLite in tests) because they lock to the first provider's model.
    private static Task<int?> GetUserIdByPublicIdAsync(SnakkDbContext ctx, string publicId, CancellationToken ct) =>
        ctx.Users
            .Where(u => u.PublicId == publicId)
            .Select(u => (int?)u.Id)
            .FirstOrDefaultAsync(ct);

    private static Task<int?> GetCommunityIdByPublicIdAsync(SnakkDbContext ctx, string publicId, CancellationToken ct) =>
        ctx.Communities
            .Where(c => c.PublicId == publicId)
            .Select(c => (int?)c.Id)
            .FirstOrDefaultAsync(ct);

    private static Task<HubScope?> GetHubScopeByPublicIdAsync(SnakkDbContext ctx, string publicId, CancellationToken ct) =>
        ctx.Hubs
            .Where(h => h.PublicId == publicId)
            .Select(h => new HubScope(h.Id, h.CommunityId))
            .FirstOrDefaultAsync(ct);

    private static Task<SpaceScope?> GetSpaceScopeByPublicIdAsync(SnakkDbContext ctx, string publicId, CancellationToken ct) =>
        ctx.Spaces
            .Where(s => s.PublicId == publicId)
            .Select(s => new SpaceScope(s.Id, s.HubId, s.Hub.CommunityId))
            .FirstOrDefaultAsync(ct);

    private static Task<ScopeIds?> GetDiscussionScopeByPublicIdAsync(SnakkDbContext ctx, string publicId, CancellationToken ct) =>
        ctx.Discussions
            .Where(d => d.PublicId == publicId)
            .Select(d => new ScopeIds(d.SpaceId, d.HubId, d.CommunityId))
            .FirstOrDefaultAsync(ct);

    private static Task<ScopeIds?> GetPostScopeByPublicIdAsync(SnakkDbContext ctx, string publicId, CancellationToken ct) =>
        ctx.Posts
            .Where(p => p.PublicId == publicId)
            .Select(p => new ScopeIds(p.SpaceId, p.HubId, p.CommunityId))
            .FirstOrDefaultAsync(ct);

    public async Task<bool> UserHasPermissionAsync(
        string userId,
        string permissionName,
        string? scope = null,
        string? scopePublicId = null,
        CancellationToken ct = default)
    {
        // Get user's database ID from PublicId
        var userDbId = await GetUserIdByPublicIdAsync(context, userId, ct);

        if (userDbId is null)
            return false;

        // Get all active roles for the user (including temporary elevations)
        var userRoles = await GetUserRolesWithScopeAsync(userDbId.Value, ct);

        // HIERARCHY: GlobalAdmin has access to EVERYTHING
        if (userRoles.Any(r => r.RoleType == "GlobalAdmin"))
        {
            logger.LogDebug("User {UserId} has GlobalAdmin role - granting access to {Permission}", userId, permissionName);
            return true;
        }

        // If scope is specified, check hierarchical access
        if (!string.IsNullOrEmpty(scope) && !string.IsNullOrEmpty(scopePublicId))
        {
            var hasHierarchicalAccess = await CheckHierarchicalAccessAsync(userRoles, permissionName, scope, scopePublicId, ct);

            if (hasHierarchicalAccess)
            {
                logger.LogDebug("User {UserId} has hierarchical access to {Scope}:{ScopePublicId}", userId, scope, scopePublicId);
                return true;
            }
        }

        // Finally, check explicit permissions for the user's roles
        var cacheKey = $"user_permissions_{userId}";

        var userPermissions = await cache.GetOrCreateAsync(
            cacheKey,
            async cancel => await GetUserPermissionsInternalAsync(userId, cancel),
            CacheOptions,
            cancellationToken: ct);

        return userPermissions.Any(p => p.Name == permissionName);
    }

    private async Task<List<UserRoleScope>> GetUserRolesWithScopeAsync(int userId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        var rolesTask = ReadAsync(db => db.UserRoles
            .Where(ur => ur.UserId == userId && ur.RevokedAt == null)
            .Select(ur => new UserRoleScope
            {
                RoleType = ((UserRoleTypeEnum)ur.RoleId).ToString(),
                CommunityId = ur.CommunityId,
                HubId = ur.HubId,
                SpaceId = ur.SpaceId
            })
            .ToListAsync(ct));

        var tempRolesTask = ReadAsync(db => db.TemporaryRoleElevations
            .Where(e => e.UserId == userId && e.RevokedAt == null && e.ExpiresAt > now)
            .Select(e => new UserRoleScope
            {
                RoleType = e.RoleType,
                CommunityId = e.Scope == "Community" ? e.ScopeId : (int?)null,
                HubId = e.Scope == "Hub" ? e.ScopeId : (int?)null,
                SpaceId = e.Scope == "Space" ? e.ScopeId : (int?)null
            })
            .ToListAsync(ct));

        await Task.WhenAll(rolesTask, tempRolesTask);
        var roles = rolesTask.Result;
        roles.AddRange(tempRolesTask.Result);

        return roles;
    }

    // ManageCommunity is an admin-only operation; CommunityMod does not qualify.
    private static bool IsAdminOnlyPermission(string permissionName) =>
        permissionName is "ManageCommunity";

    private async Task<bool> CheckHierarchicalAccessAsync(
        List<UserRoleScope> userRoles,
        string permissionName,
        string scope,
        string scopePublicId,
        CancellationToken ct = default)
    {
        var adminOnly = IsAdminOnlyPermission(permissionName);

        switch (scope.ToLower())
        {
            case "community":
                var communityId = await GetCommunityIdByPublicIdAsync(context, scopePublicId, ct);
                if (communityId is null) return false;

                return userRoles.Any(r =>
                    r.RoleType == "CommunityAdmin" && r.CommunityId == communityId.Value)
                    || (!adminOnly && userRoles.Any(r =>
                        r.RoleType == "CommunityMod" && r.CommunityId == communityId.Value));

            case "hub":
                var hub = await GetHubScopeByPublicIdAsync(context, scopePublicId, ct);
                if (hub is null) return false;

                return userRoles.Any(r =>
                    (r.RoleType == "HubMod" && r.HubId == hub.Id)
                    || (r.RoleType == "CommunityAdmin" && r.CommunityId == hub.CommunityId)
                    || (!adminOnly && r.RoleType == "CommunityMod" && r.CommunityId == hub.CommunityId));

            case "space":
                var space = await GetSpaceScopeByPublicIdAsync(context, scopePublicId, ct);
                if (space is null) return false;

                return userRoles.Any(r =>
                    (r.RoleType == "SpaceMod" && r.SpaceId == space.Id)
                    || (r.RoleType == "HubMod" && r.HubId == space.HubId)
                    || (r.RoleType == "CommunityAdmin" && r.CommunityId == space.CommunityId)
                    || (!adminOnly && r.RoleType == "CommunityMod" && r.CommunityId == space.CommunityId));

            case "discussion":
                var discussion = await GetDiscussionScopeByPublicIdAsync(context, scopePublicId, ct);
                if (discussion is null) return false;

                return userRoles.Any(r =>
                    (r.RoleType == "SpaceMod" && r.SpaceId == discussion.SpaceId)
                    || (r.RoleType == "HubMod" && r.HubId == discussion.HubId)
                    || (r.RoleType == "CommunityAdmin" && r.CommunityId == discussion.CommunityId)
                    || (!adminOnly && r.RoleType == "CommunityMod" && r.CommunityId == discussion.CommunityId));

            case "post":
                var post = await GetPostScopeByPublicIdAsync(context, scopePublicId, ct);
                if (post is null) return false;

                return userRoles.Any(r =>
                    (r.RoleType == "SpaceMod" && r.SpaceId == post.SpaceId)
                    || (r.RoleType == "HubMod" && r.HubId == post.HubId)
                    || (r.RoleType == "CommunityAdmin" && r.CommunityId == post.CommunityId)
                    || (!adminOnly && r.RoleType == "CommunityMod" && r.CommunityId == post.CommunityId));

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

    public async Task<List<PermissionDto>> GetUserPermissionsAsync(string userId, CancellationToken ct = default)
    {
        var cacheKey = $"user_permissions_{userId}";

        var permissions = await cache.GetOrCreateAsync(
            cacheKey,
            async cancel => await GetUserPermissionsInternalAsync(userId, cancel),
            CacheOptions,
            cancellationToken: ct);

        return permissions ?? [];
    }

    private async Task<List<PermissionDto>> GetUserPermissionsInternalAsync(string userId, CancellationToken ct = default)
    {
        // Get user's database ID from PublicId
        var userDbId = await GetUserIdByPublicIdAsync(context, userId, ct);

        if (userDbId is null)
            return [];

        // Get all active roles for the user
        var userRoleIds = await context.UserRoles
            .Where(ur =>
                ur.UserId == userDbId.Value
                && ur.RevokedAt == null)
            .Select(ur => ur.Id)
            .ToListAsync(ct);

        if (userRoleIds.Count == 0)
            return [];

        // Get all permissions assigned to those roles
        var permissions = await context.RolePermissions
            .Where(rp => userRoleIds.Contains(rp.RoleId))
            .Select(rp => rp.Permission)
            .Distinct()
            .Select(p => new PermissionDto
            {
                PublicId = p!.PublicId,
                Name = p.Name,
                Category = p.Category,
                Description = p.Description,
                IsSystemPermission = p.IsSystemPermission,
                CreatedAt = p.CreatedAt
            })
            .ToListAsync(ct);

        return permissions;
    }

    public async Task<List<PermissionDto>> GetRolePermissionsAsync(int roleId, CancellationToken ct = default) =>
        await context.RolePermissions
            .Where(rp => rp.RoleId == roleId)
            .Select(rp => new PermissionDto
            {
                PublicId = rp.Permission!.PublicId,
                Name = rp.Permission.Name,
                Category = rp.Permission.Category,
                Description = rp.Permission.Description,
                IsSystemPermission = rp.Permission.IsSystemPermission,
                CreatedAt = rp.Permission.CreatedAt
            })
            .ToListAsync(ct);

    public async Task<List<PermissionDto>> GetAllPermissionsAsync(CancellationToken ct = default)
    {
        var cacheKey = "all_permissions";

        var permissions = await cache.GetOrCreateAsync(
            cacheKey,
            async cancel => await context.Permissions
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
                .ToListAsync(cancel),
            CacheOptions,
            cancellationToken: ct);

        return permissions ?? [];
    }

    public async Task AssignPermissionToRoleAsync(int roleId, int permissionId, string adminUserId, CancellationToken ct = default)
    {
        // Check if the assignment already exists
        var exists = await context.RolePermissions
            .AnyAsync(rp =>
                rp.RoleId == roleId
                && rp.PermissionId == permissionId, ct);

        if (exists)
        {
            logger.LogWarning("Permission {PermissionId} already assigned to role {RoleId}", permissionId, roleId);
            return;
        }

        // Get admin user database ID
        var adminDbId = await GetUserIdByPublicIdAsync(context, adminUserId, ct);

        var rolePermission = new RolePermissionDatabaseEntity
        {
            RoleId = roleId,
            PermissionId = permissionId,
            GrantedAt = DateTime.UtcNow,
            GrantedById = adminDbId
        };

        context.RolePermissions.Add(rolePermission);
        await context.SaveChangesAsync(ct);

        // Invalidate cache for all users with this role
        await InvalidateRoleCacheAsync(roleId, ct);

        // Log audit event
        var permission = await context.Permissions.FindAsync([permissionId], ct);
        var role = await context.UserRoles.FirstOrDefaultAsync(r => r.Id == roleId, ct);
        var roleName = role is not null ? ((UserRoleTypeEnum)role.RoleId).ToString() : "Unknown";

        await securityService.LogAuditAsync(
            adminUserId,
            "AssignPermission",
            $"Assigned permission '{permission?.Name}' to role '{roleName}'",
            "Permission",
            permissionId.ToString());

        logger.LogInformation(
            "Assigned permission {PermissionId} to role {RoleId} by admin {AdminUserId}",
            permissionId,
            roleId,
            adminUserId);
    }

    public async Task RevokePermissionFromRoleAsync(int roleId, int permissionId, string adminUserId, CancellationToken ct = default)
    {
        var rolePermission = await context.RolePermissions
            .FirstOrDefaultAsync(rp =>
                rp.RoleId == roleId
                && rp.PermissionId == permissionId, ct);

        if (rolePermission is null)
        {
            logger.LogWarning("Permission {PermissionId} not found for role {RoleId}", permissionId, roleId);
            return;
        }

        context.RolePermissions.Remove(rolePermission);
        await context.SaveChangesAsync(ct);

        // Invalidate cache for all users with this role
        await InvalidateRoleCacheAsync(roleId, ct);

        // Log audit event
        var permission = await context.Permissions.FindAsync([permissionId], ct);
        var role = await context.UserRoles.FirstOrDefaultAsync(r => r.Id == roleId, ct);
        var roleName = role is not null ? ((UserRoleTypeEnum)role.RoleId).ToString() : "Unknown";

        await securityService.LogAuditAsync(
            adminUserId,
            "RevokePermission",
            $"Revoked permission '{permission?.Name}' from role '{roleName}'",
            "Permission",
            permissionId.ToString());

        logger.LogInformation(
            "Revoked permission {PermissionId} from role {RoleId} by admin {AdminUserId}",
            permissionId,
            roleId,
            adminUserId);
    }

    public async Task<TemporaryRoleElevationDto> GrantTemporaryRoleAsync(
        string userId,
        string roleType,
        string scope,
        int scopeId,
        DateTime expiresAt,
        string reason,
        string adminUserId,
        CancellationToken ct = default)
    {
        // Get user and admin database IDs
        var user = await context.Users
            .Where(u => u.PublicId == userId)
            .Select(u => new {
                u.Id,
                u.PublicId,
                u.DisplayName })
            .FirstOrDefaultAsync(ct);

        if (user is null)
            throw new InvalidOperationException($"User {userId} not found");

        var adminUser = await context.Users
            .Where(a => a.PublicId == adminUserId)
            .Select(a => new {
                a.Id,
                a.DisplayName })
            .FirstOrDefaultAsync(ct);

        if (adminUser is null)
            throw new InvalidOperationException($"Admin user {adminUserId} not found");

        var elevation = new TemporaryRoleElevationDatabaseEntity
        {
            PublicId = Ulid.NewUlid().ToString(),
            UserId = user.Id,
            RoleType = roleType,
            Scope = scope,
            ScopeId = scopeId,
            ExpiresAt = expiresAt,
            Reason = reason,
            GrantedById = adminUser.Id,
            CreatedAt = DateTime.UtcNow
        };

        context.TemporaryRoleElevations.Add(elevation);
        await context.SaveChangesAsync(ct);

        // Invalidate user's permission cache
        await cache.RemoveAsync($"user_permissions_{userId}", ct);
        await cache.RemoveByTagAsync($"manage_perms_user_{userId}", ct);

        // Log audit event
        await securityService.LogAuditAsync(
            adminUserId,
            "GrantTemporaryRole",
            $"Granted temporary {roleType} role to user {user.DisplayName} in {scope}:{scopeId}, expires at {expiresAt:u}",
            "TemporaryRole",
            elevation.PublicId);

        logger.LogInformation(
            "Granted temporary role {RoleType} to user {UserId} by admin {AdminUserId}, expires at {ExpiresAt}",
            roleType,
            userId,
            adminUserId,
            expiresAt);

        return new TemporaryRoleElevationDto
        {
            PublicId = elevation.PublicId,
            UserPublicId = user.PublicId,
            UserDisplayName = user.DisplayName ?? "",
            RoleType = roleType,
            Scope = scope,
            ScopePublicId = await ResolveScopePublicIdAsync(scope, scopeId, ct),
            ExpiresAt = expiresAt,
            Reason = reason,
            GrantedByDisplayName = adminUser.DisplayName ?? "",
            CreatedAt = elevation.CreatedAt
        };
    }

    public async Task RevokeTemporaryRoleAsync(string elevationId, string reason, string adminUserId, CancellationToken ct = default)
    {
        var elevation = await context.TemporaryRoleElevations
            .AsTracking()
            .Include(e => e.User)
            .FirstOrDefaultAsync(e =>
                e.PublicId == elevationId
                && e.RevokedAt == null, ct);

        if (elevation is null)
        {
            logger.LogWarning("Temporary role elevation {ElevationId} not found or already revoked", elevationId);
            return;
        }

        // Get admin user database ID
        var adminDbId = await GetUserIdByPublicIdAsync(context, adminUserId, ct);

        elevation.RevokedAt = DateTime.UtcNow;
        elevation.RevokedById = adminDbId;
        elevation.RevokedReason = reason;

        await context.SaveChangesAsync(ct);

        // Invalidate user's permission cache
        await cache.RemoveAsync($"user_permissions_{elevation.User!.PublicId}", ct);
        await cache.RemoveByTagAsync($"manage_perms_user_{elevation.User!.PublicId}", ct);

        // Log audit event
        await securityService.LogAuditAsync(
            adminUserId,
            "RevokeTemporaryRole",
            $"Revoked temporary role elevation for user {elevation.User.DisplayName}. Reason: {reason}",
            "TemporaryRole",
            elevationId);

        logger.LogInformation("Revoked temporary role elevation {ElevationId} by admin {AdminUserId}", elevationId, adminUserId);
    }

    public async Task<List<TemporaryRoleElevationDto>> GetActiveTemporaryRolesAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        var elevations = await context.TemporaryRoleElevations
            .Where(e =>
                e.RevokedAt == null
                && e.ExpiresAt > now)
            .OrderByDescending(e => e.CreatedAt)
            .Include(e => e.User)
            .Include(e => e.GrantedBy)
            .Include(e => e.RevokedBy)
            .ToListAsync(ct);

        var scopePublicIds = await BulkResolveScopePublicIdsAsync(elevations.Select(e => (e.Scope, e.ScopeId)), ct);

        return elevations.Select(e => new TemporaryRoleElevationDto
        {
            PublicId = e.PublicId,
            UserPublicId = e.User!.PublicId,
            UserDisplayName = e.User.DisplayName ?? "",
            RoleType = e.RoleType,
            Scope = e.Scope,
            ScopePublicId = scopePublicIds.GetValueOrDefault((e.Scope, e.ScopeId), ""),
            ExpiresAt = e.ExpiresAt,
            Reason = e.Reason,
            GrantedByDisplayName = e.GrantedBy!.DisplayName ?? "",
            RevokedAt = e.RevokedAt,
            RevokedByDisplayName = e.RevokedBy?.DisplayName,
            RevokedReason = e.RevokedReason,
            CreatedAt = e.CreatedAt
        }).ToList();
    }

    public async Task<List<TemporaryRoleElevationDto>> GetUserTemporaryRolesAsync(string userId, CancellationToken ct = default)
    {
        var userDbId = await GetUserIdByPublicIdAsync(context, userId, ct);

        if (userDbId is null)
            return [];

        var elevations = await context.TemporaryRoleElevations
            .Where(e => e.UserId == userDbId.Value)
            .OrderByDescending(e => e.CreatedAt)
            .Include(e => e.User)
            .Include(e => e.GrantedBy)
            .Include(e => e.RevokedBy)
            .ToListAsync(ct);

        var scopePublicIds = await BulkResolveScopePublicIdsAsync(elevations.Select(e => (e.Scope, e.ScopeId)), ct);

        return elevations.Select(e => new TemporaryRoleElevationDto
        {
            PublicId = e.PublicId,
            UserPublicId = e.User!.PublicId,
            UserDisplayName = e.User.DisplayName ?? "",
            RoleType = e.RoleType,
            Scope = e.Scope,
            ScopePublicId = scopePublicIds.GetValueOrDefault((e.Scope, e.ScopeId), ""),
            ExpiresAt = e.ExpiresAt,
            Reason = e.Reason,
            GrantedByDisplayName = e.GrantedBy!.DisplayName ?? "",
            RevokedAt = e.RevokedAt,
            RevokedByDisplayName = e.RevokedBy?.DisplayName,
            RevokedReason = e.RevokedReason,
            CreatedAt = e.CreatedAt
        }).ToList();
    }

    private async Task<Dictionary<(string Scope, int ScopeId), string>> BulkResolveScopePublicIdsAsync(
        IEnumerable<(string Scope, int ScopeId)> scopePairs,
        CancellationToken ct = default)
    {
        var pairs = scopePairs.Distinct().ToList();

        var communityIds = pairs.Where(p => p.Scope == "Community").Select(p => p.ScopeId).ToList();
        var hubIds       = pairs.Where(p => p.Scope == "Hub").Select(p => p.ScopeId).ToList();
        var spaceIds     = pairs.Where(p => p.Scope == "Space").Select(p => p.ScopeId).ToList();

        var tasks = new List<Task<List<(string Key, int Id, string PublicId)>>>();

        if (communityIds.Count > 0)
            tasks.Add(ReadAsync(db => db.Communities.Where(c => communityIds.Contains(c.Id))
                .Select(c => new { c.Id, c.PublicId }).ToListAsync(ct)
                .ContinueWith(t => t.Result.Select(c => ("Community", c.Id, c.PublicId)).ToList())));

        if (hubIds.Count > 0)
            tasks.Add(ReadAsync(db => db.Hubs.Where(h => hubIds.Contains(h.Id))
                .Select(h => new { h.Id, h.PublicId }).ToListAsync(ct)
                .ContinueWith(t => t.Result.Select(h => ("Hub", h.Id, h.PublicId)).ToList())));

        if (spaceIds.Count > 0)
            tasks.Add(ReadAsync(db => db.Spaces.Where(s => spaceIds.Contains(s.Id))
                .Select(s => new { s.Id, s.PublicId }).ToListAsync(ct)
                .ContinueWith(t => t.Result.Select(s => ("Space", s.Id, s.PublicId)).ToList())));

        await Task.WhenAll(tasks);

        var result = new Dictionary<(string, int), string>();
        foreach (var row in tasks.SelectMany(t => t.Result))
            result[(row.Key, row.Id)] = row.PublicId;

        return result;
    }

    private async Task<string> ResolveScopePublicIdAsync(string scope, int scopeId, CancellationToken ct = default) =>
        scope switch
        {
            "Community" => await context.Communities
                .Where(c => c.Id == scopeId)
                .Select(c => c.PublicId)
                .FirstOrDefaultAsync(ct) ?? "",

            "Hub" => await context.Hubs
                .Where(h => h.Id == scopeId)
                .Select(h => h.PublicId)
                .FirstOrDefaultAsync(ct) ?? "",

            "Space" => await context.Spaces
                .Where(s => s.Id == scopeId)
                .Select(s => s.PublicId)
                .FirstOrDefaultAsync(ct) ?? "",

            _ => ""
        };

    private async Task InvalidateRoleCacheAsync(int roleId, CancellationToken ct = default)
    {
        // roleId is the UserRole.Id (specific assignment PK) — RolePermissions.RoleId references it
        var userIds = await context.UserRoles
            .Where(ur => ur.Id == roleId)
            .Select(ur => ur.User.PublicId)
            .ToListAsync(ct);

        // Invalidate cache for each user
        foreach (var userId in userIds)
        {
            await cache.RemoveAsync($"user_permissions_{userId}", ct);
            await cache.RemoveByTagAsync($"manage_perms_user_{userId}", ct);
        }
    }
}
