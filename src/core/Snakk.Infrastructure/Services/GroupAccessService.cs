using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Snakk.Application.DTOs.Management;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Shared.Enums;

namespace Snakk.Infrastructure.Services;

public class GroupAccessService(
    SnakkDbContext context,
    HybridCache cache,
    IUserGrantsCacheService grantsCache) : IGroupAccessService
{
    // ==================== Public API ====================

    public async Task<GroupAccessResult> CheckAccessAsync(
        string? userPublicId,
        string communityPublicId,
        string? hubPublicId = null,
        string? spacePublicId = null,
        CancellationToken ct = default)
    {
        var cacheKey = $"group-access:{userPublicId ?? "anon"}:{communityPublicId}:{hubPublicId ?? ""}:{spacePublicId ?? ""}";
        var tags = BuildTags(userPublicId, communityPublicId, hubPublicId, spacePublicId);

        return await cache.GetOrCreateAsync(
            cacheKey,
            ct => ComputeAccessAsync(userPublicId, communityPublicId, hubPublicId, spacePublicId, ct),
            new HybridCacheEntryOptions { Expiration = TimeSpan.FromMinutes(3) },
            tags: tags,
            cancellationToken: ct);
    }

    public async Task<GroupAccessResult> CheckAccessForSpaceAsync(
        string? userPublicId,
        string spacePublicId,
        CancellationToken ct = default)
    {
        var hierarchy = await context.Spaces
            .Where(s => s.PublicId == spacePublicId)
            .Select(s => new { CommunityPublicId = s.CommunityPublicId, HubPublicId = s.HubPublicId })
            .FirstOrDefaultAsync(ct);

        if (hierarchy is null)
            return GroupAccessResult.Denied;

        return await CheckAccessAsync(userPublicId, hierarchy.CommunityPublicId, hierarchy.HubPublicId, spacePublicId, ct);
    }

    public async Task<(GroupAccessResult Access, string? DiscussionAuthorPublicId)> CheckAccessForDiscussionAsync(
        string? userPublicId,
        string discussionPublicId,
        CancellationToken ct = default)
    {
        var info = await context.Discussions
            .Where(d => d.PublicId == discussionPublicId)
            .Select(d => new
            {
                SpacePublicId = d.SpacePublicId,
                HubPublicId = d.HubPublicId,
                CommunityPublicId = d.CommunityPublicId,
                AuthorPublicId = d.CreatedByUserPublicId
            })
            .FirstOrDefaultAsync(ct);

        if (info is null)
            return (GroupAccessResult.Denied, null);

        var access = await CheckAccessAsync(userPublicId, info.CommunityPublicId, info.HubPublicId, info.SpacePublicId, ct);
        return (access, info.AuthorPublicId);
    }

    public async Task<EntityAccessDto> GetEntityAccessAsync(
        string? communityPublicId = null,
        string? hubPublicId = null,
        string? spacePublicId = null,
        CancellationToken ct = default)
    {
        if (spacePublicId is not null)
        {
            var space = await context.Spaces
                .Where(s => s.PublicId == spacePublicId)
                .Select(s => new { s.Id, s.IsRestricted })
                .FirstOrDefaultAsync(ct);

            if (space is null)
                return new EntityAccessDto();

            var grants = await context.GroupAccess
                .Where(a => a.SpaceId == space.Id)
                .Select(a => new GroupAccessGrantDto
                {
                    GroupPublicId = a.Group.PublicId,
                    GroupName = a.Group.Name,
                    AccessLevel = (AccessLevelEnum)a.AccessLevel
                })
                .ToListAsync(ct);

            return new EntityAccessDto { IsRestricted = space.IsRestricted, Grants = grants };
        }

        if (hubPublicId is not null)
        {
            var hub = await context.Hubs
                .Where(h => h.PublicId == hubPublicId)
                .Select(h => new { h.Id, h.IsRestricted })
                .FirstOrDefaultAsync(ct);

            if (hub is null)
                return new EntityAccessDto();

            var grants = await context.GroupAccess
                .Where(a => a.HubId == hub.Id && a.SpaceId == null)
                .Select(a => new GroupAccessGrantDto
                {
                    GroupPublicId = a.Group.PublicId,
                    GroupName = a.Group.Name,
                    AccessLevel = (AccessLevelEnum)a.AccessLevel
                })
                .ToListAsync(ct);

            return new EntityAccessDto { IsRestricted = hub.IsRestricted, Grants = grants };
        }

        if (communityPublicId is not null)
        {
            var community = await context.Communities
                .Where(c => c.PublicId == communityPublicId)
                .Select(c => new { c.Id, c.IsRestricted })
                .FirstOrDefaultAsync(ct);

            if (community is null)
                return new EntityAccessDto();

            var grants = await context.GroupAccess
                .Where(a => a.CommunityId == community.Id && a.HubId == null && a.SpaceId == null)
                .Select(a => new GroupAccessGrantDto
                {
                    GroupPublicId = a.Group.PublicId,
                    GroupName = a.Group.Name,
                    AccessLevel = (AccessLevelEnum)a.AccessLevel
                })
                .ToListAsync(ct);

            return new EntityAccessDto { IsRestricted = community.IsRestricted, Grants = grants };
        }

        return new EntityAccessDto();
    }

    public async Task SetIsRestrictedAsync(
        bool isRestricted,
        string? communityPublicId = null,
        string? hubPublicId = null,
        string? spacePublicId = null,
        CancellationToken ct = default)
    {
        if (spacePublicId is not null)
        {
            await context.Spaces
                .Where(s => s.PublicId == spacePublicId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsRestricted, isRestricted), ct);
            await cache.RemoveByTagAsync($"group-access:s:{spacePublicId}", ct);
        }
        else if (hubPublicId is not null)
        {
            await context.Hubs
                .Where(h => h.PublicId == hubPublicId)
                .ExecuteUpdateAsync(h => h.SetProperty(x => x.IsRestricted, isRestricted), ct);
            await cache.RemoveByTagAsync($"group-access:h:{hubPublicId}", ct);
        }
        else if (communityPublicId is not null)
        {
            await context.Communities
                .Where(c => c.PublicId == communityPublicId)
                .ExecuteUpdateAsync(c => c.SetProperty(x => x.IsRestricted, isRestricted), ct);
            await cache.RemoveByTagAsync($"group-access:c:{communityPublicId}", ct);
        }

        grantsCache.InvalidateRestrictedCount();
    }

    public async Task UpsertGrantAsync(
        UpsertGroupGrantRequest request,
        string? communityPublicId = null,
        string? hubPublicId = null,
        string? spacePublicId = null,
        CancellationToken ct = default)
    {
        var group = await context.Groups
            .Where(g => g.PublicId == request.GroupPublicId)
            .Select(g => new { g.Id })
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"Group {request.GroupPublicId} not found");

        GroupAccessDatabaseEntity? existing = null;

        if (spacePublicId is not null)
        {
            var spaceId = await context.Spaces
                .Where(s => s.PublicId == spacePublicId)
                .Select(s => (int?)s.Id)
                .FirstOrDefaultAsync(ct)
                ?? throw new InvalidOperationException($"Space {spacePublicId} not found");

            existing = await context.GroupAccess
                .AsTracking()
                .FirstOrDefaultAsync(a => a.GroupId == group.Id && a.SpaceId == spaceId, ct);

            if (existing is null)
            {
                context.GroupAccess.Add(new GroupAccessDatabaseEntity
                {
                    GroupId = group.Id,
                    SpaceId = spaceId,
                    AccessLevel = (int)request.AccessLevel,
                    CreatedAt = DateTime.UtcNow
                });
            }

            await cache.RemoveByTagAsync($"group-access:s:{spacePublicId}", ct);
        }
        else if (hubPublicId is not null)
        {
            var hubId = await context.Hubs
                .Where(h => h.PublicId == hubPublicId)
                .Select(h => (int?)h.Id)
                .FirstOrDefaultAsync(ct)
                ?? throw new InvalidOperationException($"Hub {hubPublicId} not found");

            existing = await context.GroupAccess
                .AsTracking()
                .FirstOrDefaultAsync(a => a.GroupId == group.Id && a.HubId == hubId && a.SpaceId == null, ct);

            if (existing is null)
            {
                context.GroupAccess.Add(new GroupAccessDatabaseEntity
                {
                    GroupId = group.Id,
                    HubId = hubId,
                    AccessLevel = (int)request.AccessLevel,
                    CreatedAt = DateTime.UtcNow
                });
            }

            await cache.RemoveByTagAsync($"group-access:h:{hubPublicId}", ct);
        }
        else if (communityPublicId is not null)
        {
            var communityId = await context.Communities
                .Where(c => c.PublicId == communityPublicId)
                .Select(c => (int?)c.Id)
                .FirstOrDefaultAsync(ct)
                ?? throw new InvalidOperationException($"Community {communityPublicId} not found");

            existing = await context.GroupAccess
                .AsTracking()
                .FirstOrDefaultAsync(a => a.GroupId == group.Id && a.CommunityId == communityId && a.HubId == null && a.SpaceId == null, ct);

            if (existing is null)
            {
                context.GroupAccess.Add(new GroupAccessDatabaseEntity
                {
                    GroupId = group.Id,
                    CommunityId = communityId,
                    AccessLevel = (int)request.AccessLevel,
                    CreatedAt = DateTime.UtcNow
                });
            }

            await cache.RemoveByTagAsync($"group-access:c:{communityPublicId}", ct);
        }

        if (existing is not null)
        {
            existing.AccessLevel = (int)request.AccessLevel;
        }

        await context.SaveChangesAsync(ct);
        await InvalidateGroupMembersAsync(group.Id, ct);
    }

    public async Task<bool> DeleteGrantAsync(
        string groupPublicId,
        string? communityPublicId = null,
        string? hubPublicId = null,
        string? spacePublicId = null,
        CancellationToken ct = default)
    {
        var group = await context.Groups
            .Where(g => g.PublicId == groupPublicId)
            .Select(g => new { g.Id })
            .FirstOrDefaultAsync(ct);

        if (group is null)
            return false;

        int deleted;

        if (spacePublicId is not null)
        {
            deleted = await context.GroupAccess
                .Where(a => a.GroupId == group.Id && a.Space!.PublicId == spacePublicId)
                .ExecuteDeleteAsync(ct);
            await cache.RemoveByTagAsync($"group-access:s:{spacePublicId}", ct);
        }
        else if (hubPublicId is not null)
        {
            deleted = await context.GroupAccess
                .Where(a => a.GroupId == group.Id && a.Hub!.PublicId == hubPublicId && a.SpaceId == null)
                .ExecuteDeleteAsync(ct);
            await cache.RemoveByTagAsync($"group-access:h:{hubPublicId}", ct);
        }
        else if (communityPublicId is not null)
        {
            deleted = await context.GroupAccess
                .Where(a => a.GroupId == group.Id && a.Community!.PublicId == communityPublicId && a.HubId == null && a.SpaceId == null)
                .ExecuteDeleteAsync(ct);
            await cache.RemoveByTagAsync($"group-access:c:{communityPublicId}", ct);
        }
        else
        {
            return false;
        }

        if (deleted > 0)
            await InvalidateGroupMembersAsync(group.Id, ct);

        return deleted > 0;
    }

    // ==================== Private helpers ====================

    /// <summary>
    /// Evicts user-grants cache entries for every member of the given group.
    /// Called after any GroupAccess change so affected users see updated access immediately.
    /// </summary>
    private async Task InvalidateGroupMembersAsync(int groupId, CancellationToken ct)
    {
        var memberIds = await context.GroupMembers
            .Where(gm => gm.GroupId == groupId)
            .Select(gm => gm.User.PublicId)
            .ToListAsync(ct);

        foreach (var userId in memberIds)
            grantsCache.Invalidate(userId);
    }

    private async ValueTask<GroupAccessResult> ComputeAccessAsync(
        string? userPublicId,
        string communityPublicId,
        string? hubPublicId,
        string? spacePublicId,
        CancellationToken ct)
    {
        // ── #1: Single scope query — load all entity IDs + IsRestricted flags ──

        int communityId;
        bool communityIsRestricted;
        int? hubId = null;
        bool hubIsRestricted = false;
        int? spaceId = null;
        bool spaceIsRestricted = false;

        if (spacePublicId is not null)
        {
            var scope = await context.Spaces
                .Where(s => s.PublicId == spacePublicId && s.CommunityPublicId == communityPublicId)
                .Select(s => new
                {
                    CommunityId = s.Hub.CommunityId,
                    CommunityIsRestricted = s.Hub.Community.IsRestricted,
                    HubId = s.HubId,
                    HubIsRestricted = s.Hub.IsRestricted,
                    SpaceId = s.Id,
                    SpaceIsRestricted = s.IsRestricted
                })
                .FirstOrDefaultAsync(ct);

            if (scope is null)
                return GroupAccessResult.Denied;

            communityId = scope.CommunityId;
            communityIsRestricted = scope.CommunityIsRestricted;
            hubId = scope.HubId;
            hubIsRestricted = scope.HubIsRestricted;
            spaceId = scope.SpaceId;
            spaceIsRestricted = scope.SpaceIsRestricted;
        }
        else if (hubPublicId is not null)
        {
            var scope = await context.Hubs
                .Where(h => h.PublicId == hubPublicId && h.CommunityPublicId == communityPublicId)
                .Select(h => new
                {
                    CommunityId = h.CommunityId,
                    CommunityIsRestricted = h.Community.IsRestricted,
                    HubId = h.Id,
                    HubIsRestricted = h.IsRestricted
                })
                .FirstOrDefaultAsync(ct);

            if (scope is null)
                return GroupAccessResult.Denied;

            communityId = scope.CommunityId;
            communityIsRestricted = scope.CommunityIsRestricted;
            hubId = scope.HubId;
            hubIsRestricted = scope.HubIsRestricted;
        }
        else
        {
            var scope = await context.Communities
                .Where(c => c.PublicId == communityPublicId)
                .Select(c => new { c.Id, c.IsRestricted })
                .FirstOrDefaultAsync(ct);

            if (scope is null)
                return GroupAccessResult.Denied;

            communityId = scope.Id;
            communityIsRestricted = scope.IsRestricted;
        }

        var anyRestricted = communityIsRestricted || hubIsRestricted || spaceIsRestricted;

        // ── Early exit: nothing restricted ──
        if (!anyRestricted)
            return GroupAccessResult.Open;

        // ── Mod bypass ──
        if (!string.IsNullOrEmpty(userPublicId))
        {
            var isMod = await context.UserRoles
                .AnyAsync(r => r.User.PublicId == userPublicId && r.RevokedAt == null, ct);
            if (isMod)
                return new GroupAccessResult { AccessLevel = AccessLevelEnum.Write, IsRestricted = true };
        }

        // ── Anonymous: no access to restricted entities ──
        if (string.IsNullOrEmpty(userPublicId))
            return GroupAccessResult.Denied;

        // ── #1: Single user group query ──
        var userGroupIds = await context.GroupMembers
            .Where(m => m.User.PublicId == userPublicId && m.Group.CommunityId == communityId)
            .Select(m => m.GroupId)
            .ToListAsync(ct);

        if (userGroupIds.Count == 0)
            return GroupAccessResult.Denied;

        // ── #1: Single grants query across all restricted levels ──
        var allGrants = await context.GroupAccess
            .Where(a => userGroupIds.Contains(a.GroupId) && (
                (communityIsRestricted && a.CommunityId == communityId && a.HubId == null && a.SpaceId == null) ||
                (hubIsRestricted && hubId.HasValue && a.HubId == hubId && a.SpaceId == null) ||
                (spaceIsRestricted && spaceId.HasValue && a.SpaceId == spaceId)
            ))
            .Select(a => new { a.AccessLevel, a.CommunityId, a.HubId, a.SpaceId })
            .ToListAsync(ct);

        // ── Intersection-gate: each restricted level must be independently satisfied ──
        int effectiveLevel = (int)AccessLevelEnum.Write;

        if (communityIsRestricted)
        {
            var grants = allGrants
                .Where(a => a.CommunityId == communityId && a.HubId == null && a.SpaceId == null)
                .ToList();
            if (grants.Count == 0)
                return GroupAccessResult.Denied;
            var gateLevel = grants.Max(g => (int)g.AccessLevel);
            effectiveLevel = Math.Min(effectiveLevel, gateLevel);
        }

        if (hubIsRestricted && hubId.HasValue)
        {
            var grants = allGrants
                .Where(a => a.HubId == hubId && a.SpaceId == null)
                .ToList();
            if (grants.Count == 0)
                return GroupAccessResult.Denied;
            var gateLevel = grants.Max(g => (int)g.AccessLevel);
            effectiveLevel = Math.Min(effectiveLevel, gateLevel);
        }

        if (spaceIsRestricted && spaceId.HasValue)
        {
            var grants = allGrants
                .Where(a => a.SpaceId == spaceId)
                .ToList();
            if (grants.Count == 0)
                return GroupAccessResult.Denied;
            var gateLevel = grants.Max(g => (int)g.AccessLevel);
            effectiveLevel = Math.Min(effectiveLevel, gateLevel);
        }

        return new GroupAccessResult { AccessLevel = (AccessLevelEnum)effectiveLevel, IsRestricted = true };
    }

    private static IEnumerable<string> BuildTags(string? userPublicId, string communityPublicId, string? hubPublicId, string? spacePublicId)
    {
        yield return $"group-access:c:{communityPublicId}";
        if (hubPublicId is not null) yield return $"group-access:h:{hubPublicId}";
        if (spacePublicId is not null) yield return $"group-access:s:{spacePublicId}";
        if (userPublicId is not null) yield return $"group-access:u:{userPublicId}";
    }
}
