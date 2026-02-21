using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;
using Snakk.Shared.Enums;

namespace Snakk.Infrastructure.Services;

public class ManagePermissionService : IManagePermissionService
{
    private readonly SnakkDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ManagePermissionService> _logger;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public ManagePermissionService(
        SnakkDbContext context,
        IMemoryCache cache,
        ILogger<ManagePermissionService> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }

    public async Task<ManagePermissionSet> GetPermissionsForScopeAsync(string userId, string scopeType, int scopeId)
    {
        var cacheKey = $"manage_perms_{userId}_{scopeType}_{scopeId}";

        var cached = await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            return await ComputePermissionsAsync(userId, scopeType, scopeId);
        });

        return cached ?? ManagePermissionSet.None;
    }

    public async Task<bool> HasPermissionAsync(string userId, string scopeType, int scopeId, ManagePermissionEnum permission)
    {
        var permissionSet = await GetPermissionsForScopeAsync(userId, scopeType, scopeId);
        return permissionSet.HasPermission(permission);
    }

    private async Task<ManagePermissionSet> ComputePermissionsAsync(string userId, string scopeType, int scopeId)
    {
        var user = await _context.Users
            .Where(u => u.PublicId == userId)
            .Select(u => new { u.Id })
            .FirstOrDefaultAsync();

        if (user == null)
            return ManagePermissionSet.None;

        // Get all active roles for the user
        var activeRoles = await _context.UserRoles
            .Where(ur => ur.UserId == user.Id && ur.RevokedAt == null)
            .Select(ur => new RoleWithScope
            {
                RoleType = (UserRoleTypeEnum)ur.RoleId,
                CommunityId = ur.CommunityId,
                HubId = ur.HubId,
                SpaceId = ur.SpaceId
            })
            .ToListAsync();

        // Also include active temporary role elevations
        var now = DateTime.UtcNow;
        var tempElevations = await _context.TemporaryRoleElevations
            .Where(e => e.UserId == user.Id && e.RevokedAt == null && e.ExpiresAt > now)
            .ToListAsync();

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

        // GlobalAdmin → ALL permissions everywhere
        if (activeRoles.Any(r => r.RoleType == UserRoleTypeEnum.GlobalAdmin))
        {
            _logger.LogDebug("User {UserId} is GlobalAdmin - granting all manage permissions", userId);
            return ManagePermissionSet.All;
        }

        // Determine the highest-level matching role for the requested scope
        var matchingRoleType = await GetHighestMatchingRoleAsync(activeRoles, scopeType, scopeId);

        if (matchingRoleType == null)
        {
            _logger.LogDebug("User {UserId} has no matching role for {ScopeType}:{ScopeId}", userId, scopeType, scopeId);
            return ManagePermissionSet.None;
        }

        return DerivePermissions(matchingRoleType.Value);
    }

    private async Task<UserRoleTypeEnum?> GetHighestMatchingRoleAsync(
        List<RoleWithScope> roles,
        string scopeType,
        int scopeId)
    {
        UserRoleTypeEnum? highest = null;

        switch (scopeType.ToLower())
        {
            case "community":
                foreach (var role in roles)
                {
                    if (role.CommunityId == scopeId)
                    {
                        if (role.RoleType == UserRoleTypeEnum.CommunityAdmin)
                            return UserRoleTypeEnum.CommunityAdmin;
                        if (role.RoleType == UserRoleTypeEnum.CommunityMod)
                            highest = UserRoleTypeEnum.CommunityMod;
                    }
                }
                break;

            case "hub":
                var hub = await _context.Hubs
                    .Where(h => h.Id == scopeId)
                    .Select(h => new { h.CommunityId })
                    .FirstOrDefaultAsync();

                if (hub == null) return null;

                foreach (var role in roles)
                {
                    // Community-level roles bubble down to hub
                    if (role.CommunityId == hub.CommunityId)
                    {
                        if (role.RoleType == UserRoleTypeEnum.CommunityAdmin)
                            return UserRoleTypeEnum.CommunityAdmin;
                        if (role.RoleType == UserRoleTypeEnum.CommunityMod && (highest == null || highest == UserRoleTypeEnum.HubMod))
                            highest = UserRoleTypeEnum.CommunityMod;
                    }

                    // Direct hub role
                    if (role.HubId == scopeId && role.RoleType == UserRoleTypeEnum.HubMod)
                    {
                        if (highest == null)
                            highest = UserRoleTypeEnum.HubMod;
                    }
                }
                break;

            case "space":
                var space = await _context.Spaces
                    .Include(s => s.Hub)
                    .Where(s => s.Id == scopeId)
                    .Select(s => new { s.HubId, s.Hub.CommunityId })
                    .FirstOrDefaultAsync();

                if (space == null) return null;

                foreach (var role in roles)
                {
                    // Community-level roles bubble down to space
                    if (role.CommunityId == space.CommunityId)
                    {
                        if (role.RoleType == UserRoleTypeEnum.CommunityAdmin)
                            return UserRoleTypeEnum.CommunityAdmin;
                        if (role.RoleType == UserRoleTypeEnum.CommunityMod &&
                            (highest == null || highest == UserRoleTypeEnum.HubMod || highest == UserRoleTypeEnum.SpaceMod))
                            highest = UserRoleTypeEnum.CommunityMod;
                    }

                    // Hub-level roles bubble down to space
                    if (role.HubId == space.HubId && role.RoleType == UserRoleTypeEnum.HubMod)
                    {
                        if (highest == null || highest == UserRoleTypeEnum.SpaceMod)
                            highest = UserRoleTypeEnum.HubMod;
                    }

                    // Direct space role
                    if (role.SpaceId == scopeId && role.RoleType == UserRoleTypeEnum.SpaceMod)
                    {
                        if (highest == null)
                            highest = UserRoleTypeEnum.SpaceMod;
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
    private static ManagePermissionSet DerivePermissions(UserRoleTypeEnum roleType)
    {
        return roleType switch
        {
            UserRoleTypeEnum.GlobalAdmin => ManagePermissionSet.All,
            UserRoleTypeEnum.CommunityAdmin => ManagePermissionSet.All,
            _ => ManagePermissionSet.Moderator
        };
    }

    private class RoleWithScope
    {
        public UserRoleTypeEnum RoleType { get; set; }
        public int? CommunityId { get; set; }
        public int? HubId { get; set; }
        public int? SpaceId { get; set; }
    }
}
