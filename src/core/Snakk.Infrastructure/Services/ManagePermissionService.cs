using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;
using Snakk.Shared.Enums;

namespace Snakk.Infrastructure.Services;

public class ManagePermissionService(
    SnakkDbContext context,
    IDbContextFactory<SnakkDbContext> dbFactory,
    HybridCache cache,
    ILogger<ManagePermissionService> logger) : IManagePermissionService
{
    private static readonly HybridCacheEntryOptions CacheOptions = new() { Expiration = TimeSpan.FromMinutes(5) };

    private async Task<T> ReadAsync<T>(Func<SnakkDbContext, Task<T>> query)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await query(db);
    }

    public async Task<ManagePermissionSet> GetPermissionsForScopeAsync(
        string userId,
        string scopeType,
        string scopePublicId,
        CancellationToken ct = default)
    {
        var cacheKey = $"manage_perms_{userId}_{scopeType}_{scopePublicId}";

        var cached = await cache.GetOrCreateAsync(
            cacheKey,
            async cancel => await ComputePermissionsAsync(userId, scopeType, scopePublicId, cancel),
            CacheOptions,
            tags: [$"manage_perms_user_{userId}"],
            cancellationToken: ct);

        return cached ?? ManagePermissionSet.None;
    }

    public async Task<bool> HasPermissionAsync(
        string userId,
        string scopeType,
        string scopePublicId,
        ManagePermissionEnum permission,
        CancellationToken ct = default)
    {
        var permissionSet = await GetPermissionsForScopeAsync(userId, scopeType, scopePublicId, ct);
        return permissionSet.HasPermission(permission);
    }

    private async Task<ManagePermissionSet> ComputePermissionsAsync(
        string userId,
        string scopeType,
        string scopePublicId,
        CancellationToken ct = default)
    {
        var user = await context.Users
            .Where(u => u.PublicId == userId)
            .Select(u => new { u.Id })
            .FirstOrDefaultAsync(ct);

        if (user is null)
            return ManagePermissionSet.None;

        // Get all active roles for the user (parallel with temp elevations)
        var now = DateTime.UtcNow;
        var userId2 = user.Id;

        var activeRolesTask = ReadAsync(db => db.UserRoles
            .Where(ur => ur.UserId == userId2 && ur.RevokedAt == null)
            .Select(ur => new RoleWithScope
            {
                RoleType = (UserRoleTypeEnum)ur.RoleId,
                CommunityId = ur.CommunityId,
                HubId = ur.HubId,
                SpaceId = ur.SpaceId
            })
            .ToListAsync(ct));

        var tempElevationsTask = ReadAsync(db => db.TemporaryRoleElevations
            .Where(e => e.UserId == userId2 && e.RevokedAt == null && e.ExpiresAt > now)
            .ToListAsync(ct));

        await Task.WhenAll(activeRolesTask, tempElevationsTask);
        var activeRoles    = activeRolesTask.Result;
        var tempElevations = tempElevationsTask.Result;

        foreach (var e in tempElevations)
        {
            if (Enum.TryParse<UserRoleTypeEnum>(e.RoleType, out var roleType))
            {
                activeRoles.Add(new RoleWithScope
                {
                    RoleType = roleType,
                    CommunityId = e.Scope == "Community" ? e.ScopeId : null,
                    HubId = e.Scope == "Hub" ? e.ScopeId : null,
                    SpaceId = e.Scope == "Space" ? e.ScopeId : null
                });
            }
        }

        // GlobalAdmin -> ALL permissions everywhere
        if (activeRoles.Count > 0 && activeRoles.Any(r => r.RoleType == UserRoleTypeEnum.GlobalAdmin))
        {
            logger.LogDebug("User {UserId} is GlobalAdmin - granting all manage permissions", userId);
            return ManagePermissionSet.All;
        }

        // Determine the highest-level matching role for the requested scope
        var matchingRoleType = await GetHighestMatchingRoleAsync(activeRoles, scopeType, scopePublicId, ct);

        if (matchingRoleType is null)
        {
            logger.LogDebug("User {UserId} has no matching role for {ScopeType}:{ScopePublicId}", userId, scopeType, scopePublicId);
            return ManagePermissionSet.None;
        }

        return DerivePermissions(matchingRoleType.Value);
    }

    private async Task<UserRoleTypeEnum?> GetHighestMatchingRoleAsync(
        List<RoleWithScope> roles,
        string scopeType,
        string scopePublicId,
        CancellationToken ct = default)
    {
        UserRoleTypeEnum? highest = null;

        switch (scopeType.ToLower())
        {
            case "community":
                // Resolve community publicId to internal ID
                var communityId = await context.Communities
                    .Where(c => c.PublicId == scopePublicId)
                    .Select(c => c.Id)
                    .FirstOrDefaultAsync(ct);

                if (communityId == 0) return null;

                foreach (var role in roles)
                {
                    if (role.CommunityId == communityId)
                    {
                        if (role.RoleType == UserRoleTypeEnum.CommunityAdmin)
                            return UserRoleTypeEnum.CommunityAdmin;

                        if (role.RoleType == UserRoleTypeEnum.CommunityMod)
                            highest = UserRoleTypeEnum.CommunityMod;
                    }
                }
                break;

            case "hub":
                // Resolve hub publicId to internal ID + parent community
                var hub = await context.Hubs
                    .Where(h => h.PublicId == scopePublicId)
                    .Select(h => new {
                        h.Id,
                        h.CommunityId })
                    .FirstOrDefaultAsync(ct);

                if (hub is null) return null;

                foreach (var role in roles)
                {
                    // Community-level roles bubble down to hub
                    if (role.CommunityId == hub.CommunityId)
                    {
                        if (role.RoleType == UserRoleTypeEnum.CommunityAdmin)
                            return UserRoleTypeEnum.CommunityAdmin;

                        if (role.RoleType == UserRoleTypeEnum.CommunityMod
                            && (highest is null || highest == UserRoleTypeEnum.HubMod))
                            highest = UserRoleTypeEnum.CommunityMod;
                    }

                    // Direct hub role
                    if (role.HubId == hub.Id && role.RoleType == UserRoleTypeEnum.HubMod)
                    {
                        highest ??= UserRoleTypeEnum.HubMod;
                    }
                }
                break;

            case "space":
                // Resolve space publicId to internal ID + parent hub/community
                var space = await context.Spaces
                    .Where(s => s.PublicId == scopePublicId)
                    .Select(s => new {
                        s.Id,
                        s.HubId,
                        s.Hub.CommunityId })
                    .FirstOrDefaultAsync(ct);

                if (space is null) return null;

                foreach (var role in roles)
                {
                    // Community-level roles bubble down to space
                    if (role.CommunityId == space.CommunityId)
                    {
                        if (role.RoleType == UserRoleTypeEnum.CommunityAdmin)
                            return UserRoleTypeEnum.CommunityAdmin;

                        if (role.RoleType == UserRoleTypeEnum.CommunityMod
                            && (highest is null || highest == UserRoleTypeEnum.HubMod || highest == UserRoleTypeEnum.SpaceMod))
                            highest = UserRoleTypeEnum.CommunityMod;
                    }

                    // Hub-level roles bubble down to space
                    if (role.HubId == space.HubId && role.RoleType == UserRoleTypeEnum.HubMod)
                    {
                        if (highest is null || highest == UserRoleTypeEnum.SpaceMod)
                            highest = UserRoleTypeEnum.HubMod;
                    }

                    // Direct space role
                    if (role.SpaceId == space.Id && role.RoleType == UserRoleTypeEnum.SpaceMod)
                    {
                        highest ??= UserRoleTypeEnum.SpaceMod;
                    }
                }
                break;
        }

        return highest;
    }

    /// <summary>
    /// Derives manage permissions from a role type.
    /// - CommunityAdmin: ALL permissions (admin of the scope)
    /// - CommunityMod, HubMod, SpaceMod: Moderator-level permissions only
    /// </summary>
    private static ManagePermissionSet DerivePermissions(UserRoleTypeEnum roleType) =>
        roleType switch
        {
            UserRoleTypeEnum.GlobalAdmin => ManagePermissionSet.All,
            UserRoleTypeEnum.CommunityAdmin => ManagePermissionSet.All,
            _ => ManagePermissionSet.Moderator
        };

    private class RoleWithScope
    {
        public UserRoleTypeEnum RoleType { get; set; }
        public int? CommunityId { get; set; }
        public int? HubId { get; set; }
        public int? SpaceId { get; set; }
    }
}
