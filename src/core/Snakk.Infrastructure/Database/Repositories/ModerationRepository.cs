namespace Snakk.Infrastructure.Database.Repositories;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Snakk.Application.Repositories;
using Snakk.Domain.ValueObjects;
using Snakk.Domain.Extensions;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Shared.Enums;
using Snakk.Shared.Models;

public class ModerationRepository(SnakkDbContext context, IDbContextFactory<SnakkDbContext> dbFactory, HybridCache cache) : IModerationRepository
{
    private readonly SnakkDbContext _context = context;
    private readonly IDbContextFactory<SnakkDbContext> _dbFactory = dbFactory;
    private readonly HybridCache _cache = cache;

    private static readonly HybridCacheEntryOptions ModRolesCacheOptions =
        new() { Expiration = TimeSpan.FromHours(24) };

    private async Task<T> ReadAsync<T>(Func<SnakkDbContext, Task<T>> query, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await query(db);
    }

    // ==================== Role Management ====================

    public async Task<UserRoleDto?> GetRoleByPublicIdAsync(string publicId, CancellationToken ct = default) => await _context.UserRoles
        .Where(ur => ur.PublicId == publicId)
        .Select(ur => new UserRoleDto(
            ur.PublicId,
            ur.User.PublicId,
            ur.User.DisplayName ?? "",
            ((UserRoleTypeEnum)ur.RoleId).ToString(),
            ur.Community != null ? ur.Community.PublicId : null,
            ur.Community != null ? ur.Community.Name : null,
            ur.Hub != null ? ur.Hub.PublicId : null,
            ur.Hub != null ? ur.Hub.Name : null,
            ur.Space != null ? ur.Space.PublicId : null,
            ur.Space != null ? ur.Space.Name : null,
            ur.AssignedByUser.PublicId,
            ur.AssignedByUser.DisplayName ?? "",
            ur.AssignedAt,
            ur.RevokedAt))
        .FirstOrDefaultAsync(ct);

    public async Task<IEnumerable<UserRoleDto>> GetActiveRolesForUserAsync(string userPublicId, CancellationToken ct = default) => await _context.UserRoles
        .Where(ur =>
            ur.User.PublicId == userPublicId
            && ur.RevokedAt == null)
        .Select(ur => new UserRoleDto(
            ur.PublicId,
            ur.User.PublicId,
            ur.User.DisplayName ?? "",
            ((UserRoleTypeEnum)ur.RoleId).ToString(),
            ur.Community != null ? ur.Community.PublicId : null,
            ur.Community != null ? ur.Community.Name : null,
            ur.Hub != null ? ur.Hub.PublicId : null,
            ur.Hub != null ? ur.Hub.Name : null,
            ur.Space != null ? ur.Space.PublicId : null,
            ur.Space != null ? ur.Space.Name : null,
            ur.AssignedByUser.PublicId,
            ur.AssignedByUser.DisplayName ?? "",
            ur.AssignedAt,
            ur.RevokedAt))
        .ToListAsync(ct);

    public async Task<IEnumerable<UserModRoleDto>> GetCachedModRolesForUserAsync(string userPublicId, CancellationToken ct = default) =>
        await _cache.GetOrCreateAsync(
            $"user-mod-roles:{userPublicId}",
            async cancel => await _context.UserRoles
                .Where(ur => ur.User.PublicId == userPublicId && ur.RevokedAt == null)
                .Select(ur => new UserModRoleDto(
                    ((UserRoleTypeEnum)ur.RoleId).ToString(),
                    ur.SpaceId != null ? "Space"
                        : ur.HubId != null ? "Hub"
                        : ur.CommunityId != null ? "Community"
                        : "Platform",
                    ur.SpaceId != null ? ur.Space!.PublicId
                        : ur.HubId != null ? ur.Hub!.PublicId
                        : ur.CommunityId != null ? ur.Community!.PublicId
                        : null,
                    ur.SpaceId != null ? ur.Space!.Name
                        : ur.HubId != null ? ur.Hub!.Name
                        : ur.CommunityId != null ? ur.Community!.Name
                        : null))
                .ToListAsync(cancel),
            ModRolesCacheOptions,
            tags: [$"manage_perms_user_{userPublicId}"],
            cancellationToken: ct) ?? [];

    public async Task<IEnumerable<UserRoleDto>> GetActiveRolesForCommunityAsync(string communityPublicId, CancellationToken ct = default) => await _context.UserRoles
        .Where(ur =>
            ur.Community != null
            && ur.Community.PublicId == communityPublicId
            && ur.RevokedAt == null)
        .Select(ur => new UserRoleDto(
            ur.PublicId,
            ur.User.PublicId,
            ur.User.DisplayName ?? "",
            ((UserRoleTypeEnum)ur.RoleId).ToString(),
            ur.Community!.PublicId,
            ur.Community.Name,
            null, null, null, null,
            ur.AssignedByUser.PublicId,
            ur.AssignedByUser.DisplayName ?? "",
            ur.AssignedAt,
            ur.RevokedAt))
        .ToListAsync(ct);

    public async Task<IEnumerable<UserRoleDto>> GetActiveRolesForHubAsync(string hubPublicId, CancellationToken ct = default) => await _context.UserRoles
        .Where(ur =>
            ur.Hub != null
            && ur.Hub.PublicId == hubPublicId
            && ur.RevokedAt == null)
        .Select(ur => new UserRoleDto(
            ur.PublicId,
            ur.User.PublicId,
            ur.User.DisplayName ?? "",
            ((UserRoleTypeEnum)ur.RoleId).ToString(),
            null, null,
            ur.Hub!.PublicId,
            ur.Hub.Name,
            null, null,
            ur.AssignedByUser.PublicId,
            ur.AssignedByUser.DisplayName ?? "",
            ur.AssignedAt,
            ur.RevokedAt))
        .ToListAsync(ct);

    public async Task<IEnumerable<UserRoleDto>> GetActiveRolesForSpaceAsync(string spacePublicId, CancellationToken ct = default) => await _context.UserRoles
        .Where(ur =>
            ur.Space != null
            && ur.Space.PublicId == spacePublicId
            && ur.RevokedAt == null)
        .Select(ur => new UserRoleDto(
            ur.PublicId,
            ur.User.PublicId,
            ur.User.DisplayName ?? "",
            ((UserRoleTypeEnum)ur.RoleId).ToString(),
            null, null, null, null,
            ur.Space!.PublicId,
            ur.Space.Name,
            ur.AssignedByUser.PublicId,
            ur.AssignedByUser.DisplayName ?? "",
            ur.AssignedAt,
            ur.RevokedAt))
        .ToListAsync(ct);

    public async Task<IEnumerable<UserRoleDto>> GetGlobalAdminsAsync(CancellationToken ct = default) => await _context.UserRoles
        .Where(ur =>
            ur.RoleId == (int)UserRoleTypeEnum.GlobalAdmin
            && ur.RevokedAt == null)
        .Select(ur => new UserRoleDto(
            ur.PublicId,
            ur.User.PublicId,
            ur.User.DisplayName ?? "",
            ((UserRoleTypeEnum)ur.RoleId).ToString(),
            null, null, null, null, null, null,
            ur.AssignedByUser.PublicId,
            ur.AssignedByUser.DisplayName ?? "",
            ur.AssignedAt,
            ur.RevokedAt))
        .ToListAsync(ct);

    public async Task<UserRoleDto> AssignRoleAsync(
        string targetUserPublicId,
        UserRoleType roleType,
        string? communityPublicId,
        string? hubPublicId,
        string? spacePublicId,
        string assignedByUserPublicId,
        CancellationToken ct = default)
    {
        var userTask = ReadAsync(db => db.Users
            .Where(u => u.PublicId == targetUserPublicId)
            .Select(u => new { u.Id, u.DisplayName })
            .FirstOrDefaultAsync(ct), ct);
        var assignerTask = ReadAsync(db => db.Users
            .Where(u => u.PublicId == assignedByUserPublicId)
            .Select(u => new { u.Id, u.DisplayName })
            .FirstOrDefaultAsync(ct), ct);
        await Task.WhenAll(userTask, assignerTask);

        var targetUser = userTask.Result ?? throw new InvalidOperationException("Target user not found");
        var assigner   = assignerTask.Result ?? throw new InvalidOperationException("Assigning user not found");

        int? communityId = null, hubId = null, spaceId = null;
        string? communityName = null, hubName = null, spaceName = null;

        if (!string.IsNullOrEmpty(spacePublicId))
        {
            var space = await _context.Spaces.Include(s => s.Hub).FirstOrDefaultAsync(s => s.PublicId == spacePublicId, ct)
                ?? throw new InvalidOperationException("Space not found");
            spaceId = space.Id;
            spaceName = space.Name;
        }
        else if (!string.IsNullOrEmpty(hubPublicId))
        {
            var hub = await _context.Hubs.FirstOrDefaultAsync(h => h.PublicId == hubPublicId, ct)
                ?? throw new InvalidOperationException("Hub not found");
            hubId = hub.Id;
            hubName = hub.Name;
        }
        else if (!string.IsNullOrEmpty(communityPublicId))
        {
            var community = await _context.Communities.FirstOrDefaultAsync(c => c.PublicId == communityPublicId, ct)
                ?? throw new InvalidOperationException("Community not found");
            communityId = community.Id;
            communityName = community.Name;
        }

        var role = new UserRoleDatabaseEntity
        {
            PublicId = Ulid.NewUlid().ToString(),
            UserId = targetUser.Id,
            AssignedByUserId = assigner.Id,
            AssignedAt = DateTime.UtcNow,

            RoleId = (int)roleType.ToShared(),

            CommunityId = roleType == UserRoleType.CommunityAdmin || roleType == UserRoleType.CommunityMod ? communityId : null,
            HubId = roleType == UserRoleType.HubMod ? hubId : null,
            SpaceId = roleType == UserRoleType.SpaceMod ? spaceId : null,
        };

        _context.UserRoles.Add(role);
        await _context.SaveChangesAsync(ct);
        await _cache.RemoveByTagAsync($"manage_perms_user_{targetUserPublicId}");

        // Bump TeamRevision on affected scope
        await BumpTeamRevisionAsync(communityPublicId, hubPublicId, spacePublicId, ct);

        // Log the action
        await LogModerationActionAsync(
            assignedByUserPublicId,
            "AssignRole",
            targetUserPublicId: targetUserPublicId,
            communityPublicId: communityPublicId,
            hubPublicId: hubPublicId,
            spacePublicId: spacePublicId,
            details: $"Assigned role {roleType}",
            ct: ct);

        return new UserRoleDto(
            role.PublicId,
            targetUserPublicId,
            targetUser.DisplayName ?? "",
            roleType.ToString(),
            communityPublicId, communityName,
            hubPublicId, hubName,
            spacePublicId, spaceName,
            assignedByUserPublicId,
            assigner.DisplayName ?? "",
            role.AssignedAt,
            null);
    }

    public async Task RevokeRoleAsync(
        string rolePublicId,
        string revokedByUserPublicId,
        CancellationToken ct = default)
    {
        var roleTask = _context.UserRoles
            .AsTracking()
            .Include(ur => ur.User)
            .Include(ur => ur.Community)
            .Include(ur => ur.Hub)
            .Include(ur => ur.Space)
            .FirstOrDefaultAsync(ur => ur.PublicId == rolePublicId, ct);
        var revokerIdTask = ReadAsync(db => db.Users
            .Where(u => u.PublicId == revokedByUserPublicId)
            .Select(u => (int?)u.Id)
            .FirstOrDefaultAsync(ct), ct);
        await Task.WhenAll(roleTask, revokerIdTask);

        var role      = roleTask.Result ?? throw new InvalidOperationException("Role not found");
        var revokerId = revokerIdTask.Result ?? throw new InvalidOperationException("Revoking user not found");

        role.RevokedAt = DateTime.UtcNow;
        role.RevokedByUserId = revokerId;

        await _context.SaveChangesAsync(ct);
        await _cache.RemoveByTagAsync($"manage_perms_user_{role.User.PublicId}");

        // Bump TeamRevision on affected scope
        await BumpTeamRevisionAsync(role.Community?.PublicId, role.Hub?.PublicId, role.Space?.PublicId, ct);

        await LogModerationActionAsync(
            revokedByUserPublicId,
            "RevokeRole",
            targetUserPublicId: role.User.PublicId,
            communityPublicId: role.Community?.PublicId,
            hubPublicId: role.Hub?.PublicId,
            spacePublicId: role.Space?.PublicId,
            details: $"Revoked role {((UserRoleTypeEnum)role.RoleId).ToString()}",
            ct: ct);
    }

    public async Task<bool> CanModerateAsync(
        string userPublicId,
        string? communityPublicId = null,
        string? hubPublicId = null,
        string? spacePublicId = null,
        CancellationToken ct = default)
    {
        var activeRoles = await _context.UserRoles
            .Where(ur =>
                ur.User.PublicId == userPublicId
                && ur.RevokedAt == null)
            .Select(ur => new {
                ur.RoleId,
                ur.HubId,
                CommunityPublicId = ur.Community != null ? ur.Community.PublicId : null,
                HubPublicId = ur.Hub != null ? ur.Hub.PublicId : null,
                SpacePublicId = ur.Space != null ? ur.Space.PublicId : null })
            .ToListAsync(ct);

        if (activeRoles.Count == 0) return false;

        // GlobalAdmin shortcut
        if (activeRoles.Any(r => r.RoleId == (int)UserRoleTypeEnum.GlobalAdmin))
            return true;

        // Community-level check
        if (!string.IsNullOrEmpty(communityPublicId)
            && activeRoles.Any(r =>
                r.CommunityPublicId == communityPublicId
                && (r.RoleId == (int)UserRoleTypeEnum.CommunityAdmin
                    || r.RoleId == (int)UserRoleTypeEnum.CommunityMod)))
            return true;

        // Hub-level check
        if (!string.IsNullOrEmpty(hubPublicId)
            && activeRoles.Any(r =>
                r.HubPublicId == hubPublicId
                && r.RoleId == (int)UserRoleTypeEnum.HubMod))
            return true;

        // Space-level check
        if (!string.IsNullOrEmpty(spacePublicId))
        {
            // Direct SpaceMod
            if (activeRoles.Any(r =>
                r.SpacePublicId == spacePublicId
                && r.RoleId == (int)UserRoleTypeEnum.SpaceMod))
                return true;

            // Hub mods can moderate spaces within their hub:
            // collect all hub db-ids from HubMod roles, then a single AnyAsync
            var hubModHubIds = activeRoles
                .Where(r => r.RoleId == (int)UserRoleTypeEnum.HubMod && r.HubId.HasValue)
                .Select(r => r.HubId!.Value)
                .ToHashSet();

            if (hubModHubIds.Count > 0
                && await _context.Spaces.AnyAsync(s =>
                    s.PublicId == spacePublicId
                    && hubModHubIds.Contains(s.HubId), ct))
                return true;
        }

        return false;
    }

    public async Task<bool> CanAdministerAsync(
        string userPublicId,
        string? communityPublicId = null,
        string? hubPublicId = null,
        string? spacePublicId = null,
        CancellationToken ct = default)
    {
        var activeRoles = await _context.UserRoles
            .Where(ur =>
                ur.User.PublicId == userPublicId
                && ur.RevokedAt == null)
            .Select(ur => new {
                ur.RoleId,
                ur.CommunityId,
                CommunityPublicId = ur.Community != null ? ur.Community.PublicId : null })
            .ToListAsync(ct);

        if (activeRoles.Count == 0) return false;

        // GlobalAdmin shortcut
        if (activeRoles.Any(r => r.RoleId == (int)UserRoleTypeEnum.GlobalAdmin))
            return true;

        // Collect CommunityAdmin db-ids and public-ids from active roles
        var communityAdminIds = activeRoles
            .Where(r => r.RoleId == (int)UserRoleTypeEnum.CommunityAdmin && r.CommunityId.HasValue)
            .Select(r => r.CommunityId!.Value)
            .ToHashSet();

        var communityAdminPublicIds = activeRoles
            .Where(r => r.RoleId == (int)UserRoleTypeEnum.CommunityAdmin && r.CommunityPublicId is not null)
            .Select(r => r.CommunityPublicId!)
            .ToHashSet();

        if (communityAdminIds.Count == 0) return false;

        // Direct community match
        if (!string.IsNullOrEmpty(communityPublicId)
            && communityAdminPublicIds.Contains(communityPublicId))
            return true;

        // Hub belongs to one of the admin's communities — single AnyAsync
        if (!string.IsNullOrEmpty(hubPublicId)
            && await _context.Hubs.AnyAsync(h =>
                h.PublicId == hubPublicId
                && communityAdminIds.Contains(h.CommunityId), ct))
            return true;

        // Space belongs (via hub) to one of the admin's communities — single AnyAsync
        if (!string.IsNullOrEmpty(spacePublicId)
            && await _context.Spaces.AnyAsync(s =>
                s.PublicId == spacePublicId
                && communityAdminIds.Contains(s.Hub.CommunityId), ct))
            return true;

        return false;
    }

    // ==================== Ban Management ====================

    public async Task<UserBanDto?> GetBanByPublicIdAsync(string publicId, CancellationToken ct = default) => await _context.UserBans
        .Where(ub => ub.PublicId == publicId)
        .Select(ub => new UserBanDto(
            ub.PublicId,
            ub.User.PublicId,
            ub.User.DisplayName ?? "",
            ((BanTypeEnum)ub.BanTypeId).ToString(),
            ub.Community != null ? ub.Community.PublicId : null,
            ub.Community != null ? ub.Community.Name : null,
            ub.Hub != null ? ub.Hub.PublicId : null,
            ub.Hub != null ? ub.Hub.Name : null,
            ub.Space != null ? ub.Space.PublicId : null,
            ub.Space != null ? ub.Space.Name : null,
            ub.Reason,
            ub.BannedAt,
            ub.ExpiresAt,
            ub.BannedByUser.PublicId,
            ub.BannedByUser.DisplayName ?? "",
            ub.UnbannedAt,
            ub.UnbannedByUser != null ? ub.UnbannedByUser.PublicId : null,
            ub.UnbannedByUser != null ? ub.UnbannedByUser.DisplayName : null))
        .FirstOrDefaultAsync(ct);

    public async Task<IEnumerable<UserBanDto>> GetActiveBansForUserAsync(string userPublicId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        return await _context.UserBans
            .Where(ub =>
                ub.User.PublicId == userPublicId
                && ub.UnbannedAt == null
                && (ub.ExpiresAt == null || ub.ExpiresAt > now))
            .Select(ub => new UserBanDto(
                ub.PublicId,
                ub.User.PublicId,
                ub.User.DisplayName ?? "",
                ((BanTypeEnum)ub.BanTypeId).ToString(),
                ub.Community != null ? ub.Community.PublicId : null,
                ub.Community != null ? ub.Community.Name : null,
                ub.Hub != null ? ub.Hub.PublicId : null,
                ub.Hub != null ? ub.Hub.Name : null,
                ub.Space != null ? ub.Space.PublicId : null,
                ub.Space != null ? ub.Space.Name : null,
                ub.Reason,
                ub.BannedAt,
                ub.ExpiresAt,
                ub.BannedByUser.PublicId,
                ub.BannedByUser.DisplayName ?? "",
                ub.UnbannedAt,
                null, null))
            .ToListAsync(ct);
    }

    public async Task<UserBanDto?> GetActiveBanForScopeAsync(
        string userPublicId,
        string? communityPublicId = null,
        string? hubPublicId = null,
        string? spacePublicId = null,
        CancellationToken ct = default)
    {
        var bans = await GetActiveBansForUserAsync(userPublicId, ct);

        if (!bans.Any()) return null;

        // Check platform-wide ban
        var platformBan = bans.FirstOrDefault(b =>
            b.CommunityPublicId == null
            && b.HubPublicId == null
            && b.SpacePublicId == null);

        if (platformBan is not null) return platformBan;

        // Resolve hierarchy: if only spacePublicId is provided, look up its parent hub and community
        if (!string.IsNullOrEmpty(spacePublicId) && string.IsNullOrEmpty(hubPublicId))
        {
            var space = await _context.Spaces
                .Include(s => s.Hub)
                .Where(s => s.PublicId == spacePublicId)
                .Select(s => new { HubPublicId = s.Hub.PublicId, CommunityPublicId = s.Hub.Community.PublicId })
                .FirstOrDefaultAsync(ct);

            if (space is not null)
            {
                hubPublicId = space.HubPublicId;
                communityPublicId = space.CommunityPublicId;
            }
        }

        // Resolve hierarchy: if only hubPublicId is provided, look up its parent community
        if (!string.IsNullOrEmpty(hubPublicId) && string.IsNullOrEmpty(communityPublicId))
        {
            var hub = await _context.Hubs
                .Where(h => h.PublicId == hubPublicId)
                .Select(h => new { CommunityPublicId = h.Community.PublicId })
                .FirstOrDefaultAsync(ct);

            if (hub is not null)
                communityPublicId = hub.CommunityPublicId;
        }

        // Check community ban
        if (!string.IsNullOrEmpty(communityPublicId))
        {
            var communityBan = bans.FirstOrDefault(b => b.CommunityPublicId == communityPublicId);

            if (communityBan is not null) return communityBan;
        }

        // Check hub ban
        if (!string.IsNullOrEmpty(hubPublicId))
        {
            var hubBan = bans.FirstOrDefault(b => b.HubPublicId == hubPublicId);

            if (hubBan is not null) return hubBan;
        }

        // Check space ban
        if (!string.IsNullOrEmpty(spacePublicId))
        {
            var spaceBan = bans.FirstOrDefault(b => b.SpacePublicId == spacePublicId);

            if (spaceBan is not null) return spaceBan;
        }

        return null;
    }

    public async Task<bool> IsUserBannedAsync(
        string userPublicId,
        string? communityPublicId = null,
        string? hubPublicId = null,
        string? spacePublicId = null,
        CancellationToken ct = default)
    {
        var ban = await GetActiveBanForScopeAsync(userPublicId, communityPublicId, hubPublicId, spacePublicId, ct);
        return ban is not null;
    }

    public async Task<UserBanDto> BanUserAsync(
        string targetUserPublicId,
        BanType banType,
        string? communityPublicId,
        string? hubPublicId,
        string? spacePublicId,
        string? reason,
        DateTime? expiresAt,
        string bannedByUserPublicId,
        CancellationToken ct = default)
    {
        var userTask   = ReadAsync(db => db.Users
            .Where(u => u.PublicId == targetUserPublicId)
            .Select(u => new { u.Id, u.DisplayName })
            .FirstOrDefaultAsync(ct), ct);
        var bannerTask = ReadAsync(db => db.Users
            .Where(u => u.PublicId == bannedByUserPublicId)
            .Select(u => new { u.Id, u.DisplayName })
            .FirstOrDefaultAsync(ct), ct);
        await Task.WhenAll(userTask, bannerTask);

        var targetUser = userTask.Result ?? throw new InvalidOperationException("Target user not found");
        var banner     = bannerTask.Result ?? throw new InvalidOperationException("Banning user not found");

        int? communityId = null, hubId = null, spaceId = null;
        string? communityName = null, hubName = null, spaceName = null;

        if (!string.IsNullOrEmpty(spacePublicId))
        {
            var space = await _context.Spaces.FirstOrDefaultAsync(s => s.PublicId == spacePublicId, ct)
                ?? throw new InvalidOperationException("Space not found");
            spaceId = space.Id;
            spaceName = space.Name;
        }
        else if (!string.IsNullOrEmpty(hubPublicId))
        {
            var hub = await _context.Hubs.FirstOrDefaultAsync(h => h.PublicId == hubPublicId, ct)
                ?? throw new InvalidOperationException("Hub not found");
            hubId = hub.Id;
            hubName = hub.Name;
        }
        else if (!string.IsNullOrEmpty(communityPublicId))
        {
            var community = await _context.Communities.FirstOrDefaultAsync(c => c.PublicId == communityPublicId, ct)
                ?? throw new InvalidOperationException("Community not found");
            communityId = community.Id;
            communityName = community.Name;
        }

        var ban = new UserBanDatabaseEntity
        {
            PublicId = Ulid.NewUlid().ToString(),
            UserId = targetUser.Id,
            CommunityId = communityId,
            HubId = hubId,
            SpaceId = spaceId,
            Reason = reason,
            BannedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt,
            BannedByUserId = banner.Id,

            BanTypeId = (int)banType.ToShared(),
        };

        _context.UserBans.Add(ban);
        await _context.SaveChangesAsync(ct);

        await LogModerationActionAsync(
            bannedByUserPublicId,
            "BanUser",
            targetUserPublicId: targetUserPublicId,
            communityPublicId: communityPublicId,
            hubPublicId: hubPublicId,
            spacePublicId: spacePublicId,
            reason: reason,
            details: $"Banned user ({banType})" + (expiresAt.HasValue ? $" until {expiresAt}" : " permanently"),
            ct: ct);

        return new UserBanDto(
            ban.PublicId,
            targetUserPublicId,
            targetUser.DisplayName ?? "",
            banType.ToString(),
            communityPublicId, communityName,
            hubPublicId, hubName,
            spacePublicId, spaceName,
            reason,
            ban.BannedAt,
            expiresAt,
            bannedByUserPublicId,
            banner.DisplayName ?? "",
            null, null, null);
    }

    public async Task UnbanUserAsync(
        string banPublicId,
        string unbannedByUserPublicId,
        CancellationToken ct = default)
    {
        var banTask      = _context.UserBans
            .AsTracking()
            .Include(ub => ub.User)
            .Include(ub => ub.Community)
            .Include(ub => ub.Hub)
            .Include(ub => ub.Space)
            .FirstOrDefaultAsync(ub => ub.PublicId == banPublicId, ct);
        var unbannerIdTask = ReadAsync(db => db.Users
            .Where(u => u.PublicId == unbannedByUserPublicId)
            .Select(u => (int?)u.Id)
            .FirstOrDefaultAsync(ct), ct);
        await Task.WhenAll(banTask, unbannerIdTask);

        var ban        = banTask.Result ?? throw new InvalidOperationException("Ban not found");
        var unbannerId = unbannerIdTask.Result ?? throw new InvalidOperationException("Unbanning user not found");

        ban.UnbannedAt = DateTime.UtcNow;
        ban.UnbannedByUserId = unbannerId;

        await _context.SaveChangesAsync(ct);

        await LogModerationActionAsync(
            unbannedByUserPublicId,
            "UnbanUser",
            targetUserPublicId: ban.User.PublicId,
            communityPublicId: ban.Community?.PublicId,
            hubPublicId: ban.Hub?.PublicId,
            spacePublicId: ban.Space?.PublicId,
            details: "Unbanned user",
            ct: ct);
    }

    // ==================== Report Management ====================

    public async Task<ReportDto?> GetReportByPublicIdAsync(string publicId, CancellationToken ct = default) => await _context.Reports
        .Where(r => r.PublicId == publicId)
        .Select(r => new ReportDto(
            r.PublicId,
            ((ReportStatusEnum)r.StatusId).ToString(),
            r.ReporterUser.PublicId,
            r.ReportedPost != null ? r.ReportedPost.PublicId : null,
            r.ReportedDiscussion != null ? r.ReportedDiscussion.PublicId : null,
            r.ReportedUser != null ? r.ReportedUser.PublicId : null,
            r.Reason != null ? r.Reason.PublicId : null,
            r.Details,
            r.CreatedAt,
            r.ResolvedAt,
            r.ResolvedByUser != null ? r.ResolvedByUser.PublicId : null,
            r.ResolutionNote))
        .FirstOrDefaultAsync(ct);

    public async Task<ReportDetailDto?> GetReportDetailByPublicIdAsync(string publicId, CancellationToken ct = default) => await _context.Reports
        .Where(r => r.PublicId == publicId)
        .Select(r => new ReportDetailDto(
            r.PublicId,
            ((ReportStatusEnum)r.StatusId).ToString(),
            r.ReporterUser.PublicId,
            r.ReporterUser.DisplayName ?? "",
            r.ReportedPost != null ? r.ReportedPost.PublicId : null,
            r.ReportedPost != null ? r.ReportedPost.Content : null,
            r.ReportedDiscussion != null ? r.ReportedDiscussion.PublicId : null,
            r.ReportedDiscussion != null ? r.ReportedDiscussion.Title : null,
            r.ReportedUser != null ? r.ReportedUser.PublicId : null,
            r.ReportedUser != null ? r.ReportedUser.DisplayName : null,
            r.Reason != null ? r.Reason.Name : null,
            r.Reason != null ? r.Reason.Description : null,
            r.Details,
            r.CreatedAt,
            r.ResolvedAt,
            r.ResolvedByUser != null ? r.ResolvedByUser.PublicId : null,
            r.ResolvedByUser != null ? r.ResolvedByUser.DisplayName : null,
            r.ResolutionNote,
            r.Space != null ? r.Space.PublicId : null,
            r.Space != null ? r.Space.Name : null,
            r.Hub != null ? r.Hub.PublicId : null,
            r.Hub != null ? r.Hub.Name : null,
            r.Community != null ? r.Community.PublicId : null,
            r.Community != null ? r.Community.Name : null,
            r.Comments
                .Where(c => !c.IsDeleted)
                .Select(c => new ReportCommentDto(
                    c.PublicId,
                    c.AuthorUser.PublicId,
                    c.AuthorUser.DisplayName ?? "",
                    c.Content,
                    c.CreatedAt,
                    c.EditedAt))))
        .FirstOrDefaultAsync(ct);

    public async Task<PagedResult<ReportListDto>> GetReportsForCommunityAsync(
        string communityPublicId,
        int? statusId,
        int offset,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _context.Reports
            .Where(r => r.Community != null && r.Community.PublicId == communityPublicId);

        if (statusId.HasValue)
            query = query.Where(r => r.StatusId == statusId.Value);

        return await GetPagedReportsAsync(query, offset, pageSize, ct);
    }

    public async Task<PagedResult<ReportListDto>> GetReportsForHubAsync(
        string hubPublicId,
        int? statusId,
        int offset,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _context.Reports
            .Where(r =>
                (r.Hub != null && r.Hub.PublicId == hubPublicId)
                || (r.Space != null && r.Space.Hub.PublicId == hubPublicId));

        if (statusId.HasValue)
            query = query.Where(r => r.StatusId == statusId.Value);

        return await GetPagedReportsAsync(query, offset, pageSize, ct);
    }

    public async Task<PagedResult<ReportListDto>> GetReportsForSpaceAsync(
        string spacePublicId,
        int? statusId,
        int offset,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _context.Reports
            .Where(r => r.Space != null && r.Space.PublicId == spacePublicId);

        if (statusId.HasValue)
            query = query.Where(r => r.StatusId == statusId.Value);

        return await GetPagedReportsAsync(query, offset, pageSize, ct);
    }

    public async Task<PagedResult<ReportListDto>> GetReportsForModeratorAsync(
        string moderatorPublicId,
        int? statusId,
        int offset,
        int pageSize,
        CancellationToken ct = default)
    {
        // Get moderator's roles
        var roles = await GetActiveRolesForUserAsync(moderatorPublicId, ct);

        if (!roles.Any())
            return new PagedResult<ReportListDto> { Items = [], Offset = offset, PageSize = pageSize, HasMoreItems = false };

        // Find highest level role (role.Role is now a string from DTO, so we still compare strings)
        var highestRole = roles.OrderBy(r => r.Role switch
        {
            "GlobalAdmin" => 0,
            "CommunityAdmin" => 1,
            "CommunityMod" => 2,
            "HubMod" => 3,
            "SpaceMod" => 4,
            _ => 5
        }).First();

        if (!string.IsNullOrEmpty(highestRole.CommunityPublicId))
            return await GetReportsForCommunityAsync(highestRole.CommunityPublicId, statusId, offset, pageSize, ct);

        if (!string.IsNullOrEmpty(highestRole.HubPublicId))
            return await GetReportsForHubAsync(highestRole.HubPublicId, statusId, offset, pageSize, ct);

        if (!string.IsNullOrEmpty(highestRole.SpacePublicId))
            return await GetReportsForSpaceAsync(highestRole.SpacePublicId, statusId, offset, pageSize, ct);

        return new PagedResult<ReportListDto> { Items = [], Offset = offset, PageSize = pageSize, HasMoreItems = false };
    }

    public async Task<int> GetPendingReportCountForModeratorAsync(string moderatorPublicId, CancellationToken ct = default)
    {
        var roles = await GetActiveRolesForUserAsync(moderatorPublicId, ct);
        if (!roles.Any()) return 0;

        var highestRole = roles.OrderBy(r => r.Role switch
        {
            "GlobalAdmin" => 0,
            "CommunityAdmin" => 1,
            "CommunityMod" => 2,
            "HubMod" => 3,
            "SpaceMod" => 4,
            _ => 5
        }).First();

        var statusId = (int)ReportStatusEnum.Pending;

        if (!string.IsNullOrEmpty(highestRole.CommunityPublicId))
            return await _context.Reports.CountAsync(r =>
                r.Community != null && r.Community.PublicId == highestRole.CommunityPublicId
                && r.StatusId == statusId, ct);

        if (!string.IsNullOrEmpty(highestRole.HubPublicId))
            return await _context.Reports.CountAsync(r =>
                ((r.Hub != null && r.Hub.PublicId == highestRole.HubPublicId)
                || (r.Space != null && r.Space.Hub.PublicId == highestRole.HubPublicId))
                && r.StatusId == statusId, ct);

        if (!string.IsNullOrEmpty(highestRole.SpacePublicId))
            return await _context.Reports.CountAsync(r =>
                r.Space != null && r.Space.PublicId == highestRole.SpacePublicId
                && r.StatusId == statusId, ct);

        return 0;
    }

    private async Task<PagedResult<ReportListDto>> GetPagedReportsAsync(
        IQueryable<ReportDatabaseEntity> query,
        int offset,
        int pageSize,
        CancellationToken ct = default)
    {
        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip(offset)
            .Take(pageSize)
            .Select(r => new ReportListDto(
                r.PublicId,
                ((ReportStatusEnum)r.StatusId).ToString(),
                r.ReporterUser.PublicId,
                r.ReporterUser.DisplayName ?? "",
                r.ReportedPost != null ? r.ReportedPost.PublicId : null,
                r.ReportedPost != null
                    ? r.ReportedPost.Content.Length > 100
                        ? r.ReportedPost.Content.Substring(0, 100) + "..."
                        : r.ReportedPost.Content
                    : null,
                r.ReportedDiscussion != null ? r.ReportedDiscussion.PublicId : null,
                r.ReportedDiscussion != null ? r.ReportedDiscussion.Title : null,
                r.ReportedUser != null ? r.ReportedUser.PublicId : null,
                r.ReportedUser != null ? r.ReportedUser.DisplayName : null,
                r.Reason != null ? r.Reason.Name : null,
                r.Details,
                r.CreatedAt,
                r.ResolvedAt,
                r.ResolvedByUser != null ? r.ResolvedByUser.PublicId : null,
                r.ResolvedByUser != null ? r.ResolvedByUser.DisplayName : null,
                r.ResolutionNote,
                r.Space != null ? r.Space.PublicId : null,
                r.Space != null ? r.Space.Name : null,
                r.Hub != null ? r.Hub.PublicId : null,
                r.Hub != null ? r.Hub.Name : null,
                r.Community != null ? r.Community.PublicId : null,
                r.Community != null ? r.Community.Name : null,
                r.Comments.Count(c => !c.IsDeleted)))
            .ToListAsync(ct);

        return new PagedResult<ReportListDto>
        {
            Items = items,
            Offset = offset,
            PageSize = pageSize,
            HasMoreItems = offset + items.Count < totalCount
        };
    }

    public async Task<ReportDto> CreateReportAsync(
        string reporterUserPublicId,
        string? reportedPostPublicId,
        string? reportedDiscussionPublicId,
        string? reportedUserPublicId,
        string? reasonPublicId,
        string? details,
        CancellationToken ct = default)
    {
        var reporter = await _context.Users.FirstOrDefaultAsync(u => u.PublicId == reporterUserPublicId, ct)
            ?? throw new InvalidOperationException("Reporter not found");

        int? reportedPostId = null, reportedDiscussionId = null, reportedUserId = null;
        int? spaceId = null, hubId = null, communityId = null;

        if (!string.IsNullOrEmpty(reportedPostPublicId))
        {
            var post = await _context.Posts
                .Where(p => p.PublicId == reportedPostPublicId)
                .Select(p => new {
                    PostId = p.Id,
                    SpaceId = p.Discussion.SpaceId,
                    HubId = p.Discussion.Space.HubId,
                    CommunityId = p.Discussion.Space.Hub.CommunityId })
                .FirstOrDefaultAsync(ct)
                ?? throw new InvalidOperationException("Reported post not found");
            reportedPostId = post.PostId;
            spaceId = post.SpaceId;
            hubId = post.HubId;
            communityId = post.CommunityId;
        }
        else if (!string.IsNullOrEmpty(reportedDiscussionPublicId))
        {
            var discussion = await _context.Discussions
                .Where(d => d.PublicId == reportedDiscussionPublicId)
                .Select(d => new {
                    DiscussionId = d.Id,
                    SpaceId = d.SpaceId,
                    HubId = d.HubId,
                    CommunityId = d.CommunityId })
                .FirstOrDefaultAsync(ct)
                ?? throw new InvalidOperationException("Reported discussion not found");
            reportedDiscussionId = discussion.DiscussionId;
            spaceId = discussion.SpaceId;
            hubId = discussion.HubId;
            communityId = discussion.CommunityId;
        }
        else if (!string.IsNullOrEmpty(reportedUserPublicId))
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.PublicId == reportedUserPublicId, ct)
                ?? throw new InvalidOperationException("Reported user not found");
            reportedUserId = user.Id;
        }

        int? reasonId = null;

        if (!string.IsNullOrEmpty(reasonPublicId))
        {
            var reason = await _context.ReportReasons.FirstOrDefaultAsync(r => r.PublicId == reasonPublicId, ct);
            reasonId = reason?.Id;
        }

        var report = new ReportDatabaseEntity
        {
            PublicId = Ulid.NewUlid().ToString(),
            ReporterUserId = reporter.Id,
            ReportedPostId = reportedPostId,
            ReportedDiscussionId = reportedDiscussionId,
            ReportedUserId = reportedUserId,
            ReasonId = reasonId,
            Details = details,
            CreatedAt = DateTime.UtcNow,
            SpaceId = spaceId,
            HubId = hubId,
            CommunityId = communityId,

            StatusId = (int)ReportStatusEnum.Pending,
        };

        _context.Reports.Add(report);
        await _context.SaveChangesAsync(ct);

        return new ReportDto(
            report.PublicId,
            "Pending",
            reporterUserPublicId,
            reportedPostPublicId,
            reportedDiscussionPublicId,
            reportedUserPublicId,
            reasonPublicId,
            details,
            report.CreatedAt,
            null, null, null);
    }

    public async Task ResolveReportAsync(
        string reportPublicId,
        string resolvedByUserPublicId,
        string? resolutionNote,
        bool dismiss,
        CancellationToken ct = default)
    {
        var reportTask     = _context.Reports
            .AsTracking()
            .Include(r => r.Community)
            .Include(r => r.Hub)
            .Include(r => r.Space)
            .FirstOrDefaultAsync(r => r.PublicId == reportPublicId, ct);
        var resolverIdTask = ReadAsync(db => db.Users
            .Where(u => u.PublicId == resolvedByUserPublicId)
            .Select(u => (int?)u.Id)
            .FirstOrDefaultAsync(ct), ct);
        await Task.WhenAll(reportTask, resolverIdTask);

        var report     = reportTask.Result ?? throw new InvalidOperationException("Report not found");
        var resolverId = resolverIdTask.Result ?? throw new InvalidOperationException("Resolver not found");

        report.StatusId = dismiss ? (int)ReportStatusEnum.Dismissed : (int)ReportStatusEnum.Resolved;
        report.ResolvedAt = DateTime.UtcNow;
        report.ResolvedByUserId = resolverId;
        report.ResolutionNote = resolutionNote;

        await _context.SaveChangesAsync(ct);

        await LogModerationActionAsync(
            resolvedByUserPublicId,
            dismiss ? "DismissReport" : "ResolveReport",
            communityPublicId: report.Community?.PublicId,
            hubPublicId: report.Hub?.PublicId,
            spacePublicId: report.Space?.PublicId,
            reason: resolutionNote,
            ct: ct);
    }

    public async Task<ReportCommentDto> AddReportCommentAsync(
        string reportPublicId,
        string authorUserPublicId,
        string content,
        CancellationToken ct = default)
    {
        var reportIdTask = ReadAsync(db => db.Reports
            .Where(r => r.PublicId == reportPublicId)
            .Select(r => (int?)r.Id)
            .FirstOrDefaultAsync(ct), ct);
        var authorTask = ReadAsync(db => db.Users
            .Where(u => u.PublicId == authorUserPublicId)
            .Select(u => new { u.Id, u.DisplayName })
            .FirstOrDefaultAsync(ct), ct);
        await Task.WhenAll(reportIdTask, authorTask);

        var reportId = reportIdTask.Result ?? throw new InvalidOperationException("Report not found");
        var author   = authorTask.Result ?? throw new InvalidOperationException("Author not found");

        var comment = new ReportCommentDatabaseEntity
        {
            PublicId = Ulid.NewUlid().ToString(),
            ReportId = reportId,
            AuthorUserId = author.Id,
            Content = content,
            CreatedAt = DateTime.UtcNow
        };

        _context.ReportComments.Add(comment);
        await _context.SaveChangesAsync(ct);

        return new ReportCommentDto(
            comment.PublicId,
            authorUserPublicId,
            author.DisplayName ?? "",
            content,
            comment.CreatedAt,
            null);
    }

    // ==================== Report Reasons ====================

    public async Task<IEnumerable<ReportReasonDto>> GetReportReasonsForScopeAsync(
        string? communityPublicId = null,
        string? hubPublicId = null,
        string? spacePublicId = null,
        CancellationToken ct = default)
    {
        var query = _context.ReportReasons.AsQueryable();

        query = query.Where(rr =>
            (rr.CommunityId == null && rr.HubId == null && rr.SpaceId == null)
            || (!string.IsNullOrEmpty(communityPublicId) && rr.Community != null && rr.Community.PublicId == communityPublicId)
            || (!string.IsNullOrEmpty(hubPublicId) && rr.Hub != null && rr.Hub.PublicId == hubPublicId)
            || (!string.IsNullOrEmpty(spacePublicId) && rr.Space != null && rr.Space.PublicId == spacePublicId));

        return await query
            .OrderBy(rr => rr.DisplayOrder)
            .ThenBy(rr => rr.Name)
            .Select(rr => new ReportReasonDto(
                rr.PublicId,
                rr.Name,
                rr.Description,
                rr.Community != null ? rr.Community.PublicId : null,
                rr.Hub != null ? rr.Hub.PublicId : null,
                rr.Space != null ? rr.Space.PublicId : null,
                rr.DisplayOrder))
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<ReportReasonDto>> GetGlobalReportReasonsAsync(CancellationToken ct = default) => await _context.ReportReasons
        .Where(rr =>
            rr.CommunityId == null
            && rr.HubId == null
            && rr.SpaceId == null)
        .OrderBy(rr => rr.DisplayOrder)
        .ThenBy(rr => rr.Name)
        .Select(rr => new ReportReasonDto(
            rr.PublicId,
            rr.Name,
            rr.Description,
            null, null, null,
            rr.DisplayOrder))
        .ToListAsync(ct);

    // ==================== Scope-Based Queries ====================

    public async Task<IEnumerable<UserBanDto>> GetActiveBansForScopeAsync(
        string scopeType, string scopePublicId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        var query = scopeType switch
        {
            "Community" => _context.UserBans
                .Where(ub => ub.Community != null && ub.Community.PublicId == scopePublicId),
            "Hub" => _context.UserBans
                .Where(ub => ub.Hub != null && ub.Hub.PublicId == scopePublicId),
            "Space" => _context.UserBans
                .Where(ub => ub.Space != null && ub.Space.PublicId == scopePublicId),
            _ => throw new ArgumentException($"Unknown scope type: {scopeType}")
        };

        return await query
            .Where(ub => ub.UnbannedAt == null && (ub.ExpiresAt == null || ub.ExpiresAt > now))
            .OrderByDescending(ub => ub.BannedAt)
            .Select(ub => new UserBanDto(
                ub.PublicId,
                ub.User.PublicId,
                ub.User.DisplayName ?? "",
                ((BanTypeEnum)ub.BanTypeId).ToString(),
                ub.Community != null ? ub.Community.PublicId : null,
                ub.Community != null ? ub.Community.Name : null,
                ub.Hub != null ? ub.Hub.PublicId : null,
                ub.Hub != null ? ub.Hub.Name : null,
                ub.Space != null ? ub.Space.PublicId : null,
                ub.Space != null ? ub.Space.Name : null,
                ub.Reason,
                ub.BannedAt,
                ub.ExpiresAt,
                ub.BannedByUser.PublicId,
                ub.BannedByUser.DisplayName ?? "",
                ub.UnbannedAt,
                null, null))
            .ToListAsync(ct);
    }

    public async Task<int> GetActiveBanCountForScopeAsync(string scopeType, string scopePublicId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        var query = scopeType switch
        {
            "Community" => _context.UserBans
                .Where(ub => ub.Community != null && ub.Community.PublicId == scopePublicId),
            "Hub" => _context.UserBans
                .Where(ub => ub.Hub != null && ub.Hub.PublicId == scopePublicId),
            "Space" => _context.UserBans
                .Where(ub => ub.Space != null && ub.Space.PublicId == scopePublicId),
            _ => throw new ArgumentException($"Unknown scope type: {scopeType}")
        };

        return await query
            .Where(ub => ub.UnbannedAt == null && (ub.ExpiresAt == null || ub.ExpiresAt > now))
            .CountAsync(ct);
    }

    public async Task<IEnumerable<UserRoleDto>> GetActiveRolesForScopeAsync(
        string scopeType, string scopePublicId, CancellationToken ct = default)
    {
        var query = scopeType switch
        {
            "Community" => _context.UserRoles
                .Where(ur => ur.Community != null && ur.Community.PublicId == scopePublicId && ur.RevokedAt == null),
            "Hub" => _context.UserRoles
                .Where(ur => ur.Hub != null && ur.Hub.PublicId == scopePublicId && ur.RevokedAt == null),
            "Space" => _context.UserRoles
                .Where(ur => ur.Space != null && ur.Space.PublicId == scopePublicId && ur.RevokedAt == null),
            _ => throw new ArgumentException($"Unknown scope type: {scopeType}")
        };

        return await query
            .Select(ur => new UserRoleDto(
                ur.PublicId,
                ur.User.PublicId,
                ur.User.DisplayName ?? "",
                ((UserRoleTypeEnum)ur.RoleId).ToString(),
                ur.Community != null ? ur.Community.PublicId : null,
                ur.Community != null ? ur.Community.Name : null,
                ur.Hub != null ? ur.Hub.PublicId : null,
                ur.Hub != null ? ur.Hub.Name : null,
                ur.Space != null ? ur.Space.PublicId : null,
                ur.Space != null ? ur.Space.Name : null,
                ur.AssignedByUser.PublicId,
                ur.AssignedByUser.DisplayName ?? "",
                ur.AssignedAt,
                ur.RevokedAt))
            .ToListAsync(ct);
    }

    public async Task<PagedResult<ReportListDto>> GetReportsForScopeAsync(
        string scopeType, string scopePublicId, int? statusId, int offset, int pageSize, CancellationToken ct = default)
    {
        return scopeType switch
        {
            "Community" => await GetReportsForCommunityAsync(scopePublicId, statusId, offset, pageSize, ct),
            "Hub" => await GetReportsForHubAsync(scopePublicId, statusId, offset, pageSize, ct),
            "Space" => await GetReportsForSpaceAsync(scopePublicId, statusId, offset, pageSize, ct),
            _ => throw new ArgumentException($"Unknown scope type: {scopeType}")
        };
    }

    public async Task<int> GetOpenReportCountForScopeAsync(string scopeType, string scopePublicId, CancellationToken ct = default)
    {
        var result = await GetReportsForScopeAsync(scopeType, scopePublicId,
            (int)ReportStatusEnum.Pending, 0, 1, ct);
        // We need count, not items — use a simpler query
        var query = scopeType switch
        {
            "Community" => _context.Reports
                .Where(r => r.Community != null && r.Community.PublicId == scopePublicId),
            "Hub" => _context.Reports
                .Where(r => (r.Hub != null && r.Hub.PublicId == scopePublicId)
                            || (r.Space != null && r.Space.Hub.PublicId == scopePublicId)),
            "Space" => _context.Reports
                .Where(r => r.Space != null && r.Space.PublicId == scopePublicId),
            _ => throw new ArgumentException($"Unknown scope type: {scopeType}")
        };

        return await query.Where(r => r.StatusId == (int)ReportStatusEnum.Pending).CountAsync(ct);
    }

    public async Task<PagedResult<ModerationLogDto>> GetModerationLogForScopeAsync(
        string scopeType, string scopePublicId, int offset, int pageSize, CancellationToken ct = default)
    {
        return scopeType switch
        {
            "Community" => await GetModerationLogForCommunityAsync(scopePublicId, offset, pageSize, ct),
            "Hub" => await GetModerationLogForHubAsync(scopePublicId, offset, pageSize, ct),
            "Space" => await GetModerationLogForSpaceAsync(scopePublicId, offset, pageSize, ct),
            _ => throw new ArgumentException($"Unknown scope type: {scopeType}")
        };
    }

    public async Task<IEnumerable<ReportReasonDto>> GetReportReasonsForExactScopeAsync(
        string scopeType, string scopePublicId, CancellationToken ct = default)
    {
        var query = scopeType switch
        {
            "Community" => _context.ReportReasons
                .Where(rr => rr.Community != null && rr.Community.PublicId == scopePublicId
                             && rr.HubId == null && rr.SpaceId == null),
            "Hub" => _context.ReportReasons
                .Where(rr => rr.Hub != null && rr.Hub.PublicId == scopePublicId
                             && rr.SpaceId == null),
            "Space" => _context.ReportReasons
                .Where(rr => rr.Space != null && rr.Space.PublicId == scopePublicId),
            _ => throw new ArgumentException($"Unknown scope type: {scopeType}")
        };

        return await query
            .OrderBy(rr => rr.DisplayOrder)
            .ThenBy(rr => rr.Name)
            .Select(rr => new ReportReasonDto(
                rr.PublicId, rr.Name, rr.Description,
                rr.Community != null ? rr.Community.PublicId : null,
                rr.Hub != null ? rr.Hub.PublicId : null,
                rr.Space != null ? rr.Space.PublicId : null,
                rr.DisplayOrder))
            .ToListAsync(ct);
    }

    public async Task ReplaceReportReasonsForScopeAsync(
        string scopeType, string scopePublicId, string userPublicId,
        IEnumerable<(string Name, string? Description, int DisplayOrder)> reasons,
        CancellationToken ct = default)
    {
        // Soft-delete existing reasons at this exact scope
        var existing = scopeType switch
        {
            "Community" => await _context.ReportReasons
                .Where(rr => rr.Community != null && rr.Community.PublicId == scopePublicId
                             && rr.HubId == null && rr.SpaceId == null)
                .ToListAsync(ct),
            "Hub" => await _context.ReportReasons
                .Where(rr => rr.Hub != null && rr.Hub.PublicId == scopePublicId
                             && rr.SpaceId == null)
                .ToListAsync(ct),
            "Space" => await _context.ReportReasons
                .Where(rr => rr.Space != null && rr.Space.PublicId == scopePublicId)
                .ToListAsync(ct),
            _ => throw new ArgumentException($"Unknown scope type: {scopeType}")
        };

        var now = DateTime.UtcNow;

        foreach (var reason in existing)
        {
            reason.IsDeleted = true;
            reason.DeletedAt = now;
        }

        // Resolve scope entity ID and user ID in parallel
        var scopeIdTask = scopeType switch
        {
            "Community" => ReadAsync(db => db.Communities
                .Where(c => c.PublicId == scopePublicId).Select(c => (int?)c.Id).FirstOrDefaultAsync(ct), ct),
            "Hub" => ReadAsync(db => db.Hubs
                .Where(h => h.PublicId == scopePublicId).Select(h => (int?)h.Id).FirstOrDefaultAsync(ct), ct),
            "Space" => ReadAsync(db => db.Spaces
                .Where(s => s.PublicId == scopePublicId).Select(s => (int?)s.Id).FirstOrDefaultAsync(ct), ct),
            _ => throw new ArgumentException($"Unknown scope type: {scopeType}")
        };

        var userIdTask = ReadAsync(db => db.Users
            .Where(u => u.PublicId == userPublicId)
            .Select(u => (int?)u.Id)
            .FirstOrDefaultAsync(ct), ct);

        await Task.WhenAll(scopeIdTask, userIdTask);

        var scopeEntityId = scopeIdTask.Result;
        int? communityId = scopeType == "Community" ? scopeEntityId : null;
        int? hubId       = scopeType == "Hub"       ? scopeEntityId : null;
        int? spaceId     = scopeType == "Space"     ? scopeEntityId : null;
        var userId = userIdTask.Result;

        // Insert new reasons
        foreach (var r in reasons)
        {
            _context.ReportReasons.Add(new ReportReasonDatabaseEntity
            {
                PublicId = Ulid.NewUlid().ToString(),
                Name = r.Name,
                Description = r.Description,
                CommunityId = communityId,
                HubId = hubId,
                SpaceId = spaceId,
                CreatedByUserId = userId,
                CreatedAt = now,
                DisplayOrder = r.DisplayOrder
            });
        }

        await _context.SaveChangesAsync(ct);
    }

    // ==================== Moderation Log ====================

    public async Task<PagedResult<ModerationLogDto>> GetModerationLogForCommunityAsync(
        string communityPublicId,
        int offset,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _context.ModerationLogs
            .Where(ml => ml.Community != null && ml.Community.PublicId == communityPublicId);

        return await GetPagedLogsAsync(query, offset, pageSize, ct);
    }

    public async Task<PagedResult<ModerationLogDto>> GetModerationLogForHubAsync(
        string hubPublicId,
        int offset,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _context.ModerationLogs
            .Where(ml => ml.Hub != null && ml.Hub.PublicId == hubPublicId);

        return await GetPagedLogsAsync(query, offset, pageSize, ct);
    }

    public async Task<PagedResult<ModerationLogDto>> GetModerationLogForSpaceAsync(
        string spacePublicId,
        int offset,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _context.ModerationLogs
            .Where(ml => ml.Space != null && ml.Space.PublicId == spacePublicId);

        return await GetPagedLogsAsync(query, offset, pageSize, ct);
    }

    public async Task<PagedResult<ModerationLogDto>> GetModerationLogByActorAsync(
        string actorUserPublicId,
        int offset,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _context.ModerationLogs
            .Where(ml => ml.ActorUser.PublicId == actorUserPublicId);

        return await GetPagedLogsAsync(query, offset, pageSize, ct);
    }

    private async Task<PagedResult<ModerationLogDto>> GetPagedLogsAsync(
        IQueryable<ModerationLogDatabaseEntity> query,
        int offset,
        int pageSize,
        CancellationToken ct = default)
    {
        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(ml => ml.CreatedAt)
            .Skip(offset)
            .Take(pageSize)
            .Select(ml => new ModerationLogDto(
                ml.PublicId,
                ml.ActorUser.PublicId,
                ml.ActorUser.DisplayName ?? "",
                ((ModerationActionEnum)ml.ActionId).ToString(),
                ml.TargetPost != null ? ml.TargetPost.PublicId : null,
                ml.TargetDiscussion != null ? ml.TargetDiscussion.PublicId : null,
                ml.TargetDiscussion != null ? ml.TargetDiscussion.Title : null,
                ml.TargetUser != null ? ml.TargetUser.PublicId : null,
                ml.TargetUser != null ? ml.TargetUser.DisplayName : null,
                ml.Community != null ? ml.Community.PublicId : null,
                ml.Community != null ? ml.Community.Name : null,
                ml.Hub != null ? ml.Hub.PublicId : null,
                ml.Hub != null ? ml.Hub.Name : null,
                ml.Space != null ? ml.Space.PublicId : null,
                ml.Space != null ? ml.Space.Name : null,
                ml.Details,
                ml.Reason,
                ml.CreatedAt))
            .ToListAsync(ct);

        return new PagedResult<ModerationLogDto>
        {
            Items = items,
            Offset = offset,
            PageSize = pageSize,
            HasMoreItems = offset + items.Count < totalCount
        };
    }

    private async Task BumpTeamRevisionAsync(
        string? communityPublicId,
        string? hubPublicId,
        string? spacePublicId,
        CancellationToken ct = default)
    {
        var newRevision = Guid.NewGuid().ToString("N")[..8];

        // Pages read TeamRevision from the cached {space|hub|community}-meta entries, not
        // from the database — without evicting the meta entry the new revision is invisible
        // until the TTL expires and the moderators panel keeps serving the stale team.
        if (!string.IsNullOrEmpty(spacePublicId))
        {
            await _context.Spaces
                .Where(s => s.PublicId == spacePublicId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.TeamRevision, newRevision), ct);
            await _cache.RemoveAsync($"space-meta:{spacePublicId}", ct);
        }
        else if (!string.IsNullOrEmpty(hubPublicId))
        {
            await _context.Hubs
                .Where(h => h.PublicId == hubPublicId)
                .ExecuteUpdateAsync(h => h.SetProperty(x => x.TeamRevision, newRevision), ct);
            await _cache.RemoveAsync($"hub-meta:{hubPublicId}", ct);
        }
        else if (!string.IsNullOrEmpty(communityPublicId))
        {
            await _context.Communities
                .Where(c => c.PublicId == communityPublicId)
                .ExecuteUpdateAsync(c => c.SetProperty(x => x.TeamRevision, newRevision), ct);
            await _cache.RemoveAsync($"community-meta:{communityPublicId}", ct);
        }
    }

    public async Task LogModerationActionAsync(
        string actorUserPublicId,
        string action,
        string? targetPostPublicId = null,
        string? targetDiscussionPublicId = null,
        string? targetUserPublicId = null,
        string? communityPublicId = null,
        string? hubPublicId = null,
        string? spacePublicId = null,
        string? details = null,
        string? reason = null,
        CancellationToken ct = default)
    {
        var actorId = await _context.Users
            .Where(u => u.PublicId == actorUserPublicId)
            .Select(u => (int?)u.Id)
            .FirstOrDefaultAsync(ct);

        if (actorId is null) return;

        var postTask       = !string.IsNullOrEmpty(targetPostPublicId)
            ? ReadAsync(db => db.Posts.Where(p => p.PublicId == targetPostPublicId).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct), ct)
            : Task.FromResult<int?>(null);
        var discussionTask = !string.IsNullOrEmpty(targetDiscussionPublicId)
            ? ReadAsync(db => db.Discussions.Where(d => d.PublicId == targetDiscussionPublicId).Select(d => (int?)d.Id).FirstOrDefaultAsync(ct), ct)
            : Task.FromResult<int?>(null);
        var targetUserTask = !string.IsNullOrEmpty(targetUserPublicId)
            ? ReadAsync(db => db.Users.Where(u => u.PublicId == targetUserPublicId).Select(u => (int?)u.Id).FirstOrDefaultAsync(ct), ct)
            : Task.FromResult<int?>(null);
        var communityTask  = !string.IsNullOrEmpty(communityPublicId)
            ? ReadAsync(db => db.Communities.Where(c => c.PublicId == communityPublicId).Select(c => (int?)c.Id).FirstOrDefaultAsync(ct), ct)
            : Task.FromResult<int?>(null);
        var hubTask        = !string.IsNullOrEmpty(hubPublicId)
            ? ReadAsync(db => db.Hubs.Where(h => h.PublicId == hubPublicId).Select(h => (int?)h.Id).FirstOrDefaultAsync(ct), ct)
            : Task.FromResult<int?>(null);
        var spaceTask      = !string.IsNullOrEmpty(spacePublicId)
            ? ReadAsync(db => db.Spaces.Where(s => s.PublicId == spacePublicId).Select(s => (int?)s.Id).FirstOrDefaultAsync(ct), ct)
            : Task.FromResult<int?>(null);

        await Task.WhenAll(postTask, discussionTask, targetUserTask, communityTask, hubTask, spaceTask);

        int? targetPostId       = postTask.Result;
        int? targetDiscussionId = discussionTask.Result;
        int? targetUserId       = targetUserTask.Result;
        int? communityId        = communityTask.Result;
        int? hubId              = hubTask.Result;
        int? spaceId            = spaceTask.Result;

        var log = new ModerationLogDatabaseEntity
        {
            PublicId = Ulid.NewUlid().ToString(),
            ActorUserId = actorId.Value,
            TargetPostId = targetPostId,
            TargetDiscussionId = targetDiscussionId,
            TargetUserId = targetUserId,
            CommunityId = communityId,
            HubId = hubId,
            SpaceId = spaceId,
            Details = details,
            Reason = reason,
            CreatedAt = DateTime.UtcNow,

            ActionId = action switch
            {
                "DeletePost" => (int)ModerationActionEnum.DeletePost,
                "DeleteDiscussion" => (int)ModerationActionEnum.DeleteDiscussion,
                "BanUser" => (int)ModerationActionEnum.BanUser,
                "UnbanUser" => (int)ModerationActionEnum.UnbanUser,
                "AssignRole" => (int)ModerationActionEnum.AssignRole,
                "RevokeRole" => (int)ModerationActionEnum.RevokeRole,
                "ResolveReport" => (int)ModerationActionEnum.ResolveReport,
                "DismissReport" => (int)ModerationActionEnum.DismissReport,
                "EditPost" => (int)ModerationActionEnum.EditPost,
                "LockDiscussion" => (int)ModerationActionEnum.LockDiscussion,
                "UnlockDiscussion" => (int)ModerationActionEnum.LockDiscussion, // Using same enum value for now
                _ => throw new ArgumentException($"Unknown moderation action: {action}", nameof(action))
            },
        };

        _context.ModerationLogs.Add(log);
        await _context.SaveChangesAsync(ct);
    }

    // ==================== Content Moderation ====================

    public async Task ModeratorDeletePostAsync(
        string postPublicId,
        string moderatorPublicId,
        string? reason,
        CancellationToken ct = default)
    {
        var post = await _context.Posts
            .AsTracking()
            .Include(p => p.Discussion)
                .ThenInclude(d => d.Space)
                    .ThenInclude(s => s.Hub)
            .FirstOrDefaultAsync(p => p.PublicId == postPublicId, ct)
            ?? throw new InvalidOperationException("Post not found");

        var canModerate = await CanModerateAsync(
            moderatorPublicId,
            post.Discussion.Space.Hub.Community?.PublicId,
            post.Discussion.Space.Hub.PublicId,
            post.Discussion.Space.PublicId,
            ct);

        if (!canModerate)
            throw new InvalidOperationException("You don't have permission to delete this post");

        post.IsDeleted = true;
        post.DeletedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);

        await LogModerationActionAsync(
            moderatorPublicId,
            "DeletePost",
            targetPostPublicId: postPublicId,
            communityPublicId: post.Discussion.Space.Hub.Community?.PublicId,
            hubPublicId: post.Discussion.Space.Hub.PublicId,
            spacePublicId: post.Discussion.Space.PublicId,
            reason: reason,
            ct: ct);
    }

    public async Task ModeratorDeleteDiscussionAsync(
        string discussionPublicId,
        string moderatorPublicId,
        string? reason,
        CancellationToken ct = default)
    {
        var discussion = await _context.Discussions
            .AsTracking()
            .Include(d => d.Space)
                .ThenInclude(s => s.Hub)
                    .ThenInclude(h => h.Community)
            .FirstOrDefaultAsync(d => d.PublicId == discussionPublicId, ct)
            ?? throw new InvalidOperationException("Discussion not found");

        var canModerate = await CanModerateAsync(
            moderatorPublicId,
            discussion.Space.Hub.Community?.PublicId,
            discussion.Space.Hub.PublicId,
            discussion.Space.PublicId,
            ct);

        if (!canModerate)
            throw new InvalidOperationException("You don't have permission to delete this discussion");

        discussion.IsDeleted = true;
        discussion.DeletedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);

        // The deleted discussion may be the cached "latest" for its space — that entry is
        // write-invalidated with a very long TTL, so a missed removal would persist for months.
        await _cache.RemoveAsync($"space-latest-discussion:{discussion.SpaceId}", ct);
        await _cache.RemoveAsync($"preview:link:{discussionPublicId}", ct);
        await _cache.RemoveAsync($"preview:images:{discussionPublicId}", ct);

        await LogModerationActionAsync(
            moderatorPublicId,
            "DeleteDiscussion",
            targetDiscussionPublicId: discussionPublicId,
            communityPublicId: discussion.Space.Hub.Community?.PublicId,
            hubPublicId: discussion.Space.Hub.PublicId,
            spacePublicId: discussion.Space.PublicId,
            reason: reason,
            ct: ct);
    }

    public async Task LockDiscussionAsync(
        string discussionPublicId,
        string moderatorPublicId,
        string? reason,
        CancellationToken ct = default)
    {
        var discussion = await _context.Discussions
            .AsTracking()
            .Include(d => d.Space)
                .ThenInclude(s => s.Hub)
                    .ThenInclude(h => h.Community)
            .FirstOrDefaultAsync(d => d.PublicId == discussionPublicId, ct)
            ?? throw new InvalidOperationException("Discussion not found");

        var canModerate = await CanModerateAsync(
            moderatorPublicId,
            discussion.Space.Hub.Community?.PublicId,
            discussion.Space.Hub.PublicId,
            discussion.Space.PublicId,
            ct);

        if (!canModerate)
            throw new InvalidOperationException("You don't have permission to lock this discussion");

        discussion.IsLocked = true;

        await _context.SaveChangesAsync(ct);

        await LogModerationActionAsync(
            moderatorPublicId,
            "LockDiscussion",
            targetDiscussionPublicId: discussionPublicId,
            communityPublicId: discussion.Space.Hub.Community?.PublicId,
            hubPublicId: discussion.Space.Hub.PublicId,
            spacePublicId: discussion.Space.PublicId,
            reason: reason,
            ct: ct);
    }

    public async Task UnlockDiscussionAsync(
        string discussionPublicId,
        string moderatorPublicId,
        CancellationToken ct = default)
    {
        var discussion = await _context.Discussions
            .AsTracking()
            .Include(d => d.Space)
                .ThenInclude(s => s.Hub)
                    .ThenInclude(h => h.Community)
            .FirstOrDefaultAsync(d => d.PublicId == discussionPublicId, ct)
            ?? throw new InvalidOperationException("Discussion not found");

        var canModerate = await CanModerateAsync(
            moderatorPublicId,
            discussion.Space.Hub.Community?.PublicId,
            discussion.Space.Hub.PublicId,
            discussion.Space.PublicId,
            ct);

        if (!canModerate)
            throw new InvalidOperationException("You don't have permission to unlock this discussion");

        discussion.IsLocked = false;

        await _context.SaveChangesAsync(ct);

        await LogModerationActionAsync(
            moderatorPublicId,
            "UnlockDiscussion",
            targetDiscussionPublicId: discussionPublicId,
            communityPublicId: discussion.Space.Hub.Community?.PublicId,
            hubPublicId: discussion.Space.Hub.PublicId,
            spacePublicId: discussion.Space.PublicId,
            ct: ct);
    }
}
