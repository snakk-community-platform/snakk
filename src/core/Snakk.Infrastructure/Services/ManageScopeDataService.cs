using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Snakk.Application.DTOs.Management;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;
using Snakk.Shared.Enums;

namespace Snakk.Infrastructure.Services;

/// <summary>
/// Infrastructure implementation of <see cref="IManageScopeDataService"/>.
/// Handles scope resolution and hierarchy lookups for manage endpoints and gRPC services.
/// </summary>
public class ManageScopeDataService(
    IDbContextFactory<SnakkDbContext> dbFactory,
    HybridCache cache) : IManageScopeDataService
{
    // ===== Scope resolution by publicId (ManageContextEndpoints) =====

    public async Task<ScopeEntityDto?> GetCommunityByPublicIdAsync(string publicId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Communities
            .Where(c => c.PublicId == publicId)
            .Select(c => new ScopeEntityDto { DbId = c.Id, PublicId = c.PublicId, Name = c.Name, Slug = c.Slug })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<ScopeEntityDto?> GetHubByPublicIdInCommunityAsync(string hubPublicId, int communityDbId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Hubs
            .Where(h => h.PublicId == hubPublicId && h.CommunityId == communityDbId)
            .Select(h => new ScopeEntityDto { DbId = h.Id, PublicId = h.PublicId, Name = h.Name, Slug = h.Slug })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<ScopeEntityDto?> GetSpaceByPublicIdInHubAsync(string spacePublicId, int hubDbId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Spaces
            .Where(s => s.PublicId == spacePublicId && s.HubId == hubDbId)
            .Select(s => new ScopeEntityDto { DbId = s.Id, PublicId = s.PublicId, Name = s.Name, Slug = s.Slug })
            .FirstOrDefaultAsync(ct);
    }

    // ===== Scope resolution by slug (ManageGrpcService) =====

    public async Task<ScopeEntityWithAvatarDto?> GetCommunityBySlugAsync(string slug, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Communities
            .Where(c => c.Slug == slug)
            .Select(c => new ScopeEntityWithAvatarDto
            {
                DbId = c.Id,
                PublicId = c.PublicId,
                Name = c.Name,
                Slug = c.Slug,
                AvatarFileName = c.AvatarFileName
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<ScopeEntityWithAvatarDto?> GetHubBySlugInCommunityAsync(string hubSlug, int communityDbId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Hubs
            .Where(h => h.Slug == hubSlug && h.CommunityId == communityDbId)
            .Select(h => new ScopeEntityWithAvatarDto
            {
                DbId = h.Id,
                PublicId = h.PublicId,
                Name = h.Name,
                Slug = h.Slug,
                AvatarFileName = h.AvatarFileName
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<ScopeEntityWithAvatarDto?> GetSpaceBySlugInHubAsync(string spaceSlug, int hubDbId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Spaces
            .Where(s => s.Slug == spaceSlug && s.HubId == hubDbId)
            .Select(s => new ScopeEntityWithAvatarDto
            {
                DbId = s.Id,
                PublicId = s.PublicId,
                Name = s.Name,
                Slug = s.Slug,
                AvatarFileName = s.AvatarFileName
            })
            .FirstOrDefaultAsync(ct);
    }

    // ===== Discord settings =====

    public async Task<SpaceDiscordSettingsDto?> GetSpaceDiscordSettingsAsync(string spacePublicId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Spaces
            .Where(s => s.PublicId == spacePublicId)
            .Select(s => new SpaceDiscordSettingsDto
            {
                DiscordWebhookUrl = s.DiscordWebhookUrl,
                DiscordChannelName = s.DiscordChannelName,
                DiscordInviteUrl = s.DiscordInviteUrl
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<bool> UpdateSpaceDiscordSettingsAsync(string spacePublicId, SpaceDiscordSettingsDto settings, CancellationToken ct = default)
    {
        // AsTracking() so the property assignments actually persist.
        // SnakkDbContext default is NoTracking — untracked reads + property mutation silently no-ops.
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var space = await db.Spaces.AsTracking()
            .FirstOrDefaultAsync(s => s.PublicId == spacePublicId, ct);

        if (space is null)
            return false;

        space.DiscordWebhookUrl = settings.DiscordWebhookUrl;
        space.DiscordChannelName = settings.DiscordChannelName;
        space.DiscordInviteUrl = settings.DiscordInviteUrl;
        await db.SaveChangesAsync(ct);
        return true;
    }

    // ===== Global admin check =====

    private const string GlobalAdminCacheKey = "roles:global-admins";
    private static readonly HybridCacheEntryOptions GlobalAdminCacheOptions = new() { Expiration = TimeSpan.FromHours(24) };

    public async Task<bool> IsGlobalAdminAsync(string userPublicId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Users
            .Where(u => u.PublicId == userPublicId)
            .Select(u => u.Roles.Any(r =>
                r.RoleId == (int)UserRoleTypeEnum.GlobalAdmin && r.RevokedAt == null))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlySet<string>> GetGlobalAdminPublicIdsAsync(CancellationToken ct = default)
    {
        return await cache.GetOrCreateAsync<HashSet<string>>(
            GlobalAdminCacheKey,
            async cancel =>
            {
                await using var db = await dbFactory.CreateDbContextAsync(cancel);
                var ids = await db.UserRoles
                    .Where(r => r.RoleId == (int)UserRoleTypeEnum.GlobalAdmin && r.RevokedAt == null)
                    .Select(r => r.User.PublicId)
                    .ToListAsync(cancel);
                return ids.ToHashSet();
            },
            GlobalAdminCacheOptions,
            cancellationToken: ct);
    }

    public async Task InvalidateGlobalAdminCacheAsync(CancellationToken ct = default)
    {
        await cache.RemoveAsync(GlobalAdminCacheKey, ct);
    }

    // ===== Parent hierarchy for inherited bans/team =====

    public async Task<HubParentDto?> GetHubParentAsync(string hubPublicId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Hubs
            .Where(h => h.PublicId == hubPublicId)
            .Select(h => new HubParentDto
            {
                CommunityPublicId = h.Community.PublicId,
                CommunityName = h.Community.Name
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<SpaceParentsDto?> GetSpaceParentsAsync(string spacePublicId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Spaces
            .Where(s => s.PublicId == spacePublicId)
            .Select(s => new SpaceParentsDto
            {
                CommunityPublicId = s.Hub.Community.PublicId,
                CommunityName = s.Hub.Community.Name,
                HubPublicId = s.Hub.PublicId,
                HubName = s.Hub.Name
            })
            .FirstOrDefaultAsync(ct);
    }

    // ===== GetReportReasons parent lookups (with full Include-style navigation) =====

    public async Task<HubWithCommunityDto?> GetHubWithCommunityAsync(string hubPublicId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Hubs
            .Include(h => h.Community)
            .Where(h => h.PublicId == hubPublicId)
            .Select(h => new HubWithCommunityDto
            {
                HubPublicId = h.PublicId,
                HubName = h.Name,
                CommunityPublicId = h.Community.PublicId,
                CommunityName = h.Community.Name
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<SpaceWithHubCommunityDto?> GetSpaceWithHubCommunityAsync(string spacePublicId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Spaces
            .Include(s => s.Hub)
            .ThenInclude(h => h.Community)
            .Where(s => s.PublicId == spacePublicId)
            .Select(s => new SpaceWithHubCommunityDto
            {
                SpacePublicId = s.PublicId,
                SpaceName = s.Name,
                HubPublicId = s.Hub.PublicId,
                HubName = s.Hub.Name,
                CommunityPublicId = s.Hub.Community.PublicId,
                CommunityName = s.Hub.Community.Name
            })
            .FirstOrDefaultAsync(ct);
    }

    // ===== User lookups =====

    public async Task<string?> FindUserPublicIdByDisplayNameAsync(string displayName, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Users
            .Where(u => u.DisplayName == displayName)
            .Select(u => u.PublicId)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<string?> GetUserDisplayNameByPublicIdAsync(string userPublicId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Users
            .Where(u => u.PublicId == userPublicId)
            .Select(u => u.DisplayName)
            .FirstOrDefaultAsync(ct);
    }

    // ===== ModerateContent: discussion space lookup =====

    public async Task<string?> GetDiscussionSpacePublicIdAsync(string discussionPublicId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Discussions
            .Where(d => d.PublicId == discussionPublicId)
            .Select(d => d.Space.PublicId)
            .FirstOrDefaultAsync(ct);
    }

    // ===== GetModerators seed data =====

    public async Task<SpaceModeratorSeedDto?> GetSpaceModeratorSeedAsync(string spacePublicId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Spaces
            .Where(s => s.PublicId == spacePublicId)
            .Select(s => new SpaceModeratorSeedDto
            {
                SpaceDbId = s.Id,
                SpaceName = s.Name,
                HubDbId = s.HubId,
                HubName = s.HubName ?? s.Hub.Name,
                CommunityDbId = s.Hub.CommunityId,
                CommunityName = s.CommunityName ?? s.Hub.Community.Name
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<HubModeratorSeedDto?> GetHubModeratorSeedAsync(string hubPublicId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Hubs
            .Where(h => h.PublicId == hubPublicId)
            .Select(h => new HubModeratorSeedDto
            {
                HubDbId = h.Id,
                HubName = h.Name,
                CommunityDbId = h.CommunityId,
                CommunityName = h.Community.Name
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<CommunityModeratorSeedDto?> GetCommunityModeratorSeedAsync(string communityPublicId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Communities
            .Where(c => c.PublicId == communityPublicId)
            .Select(c => new CommunityModeratorSeedDto
            {
                CommunityDbId = c.Id,
                CommunityName = c.Name
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<ModeratorInfoDto>> GetActiveModeratorRolesAsync(
        int? communityDbId = null,
        int? hubDbId = null,
        int? spaceDbId = null,
        bool globalOnly = false,
        CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var query = db.UserRoles.Where(ur => ur.RevokedAt == null);

        if (globalOnly)
        {
            query = query.Where(ur => ur.RoleId == (int)UserRoleTypeEnum.GlobalAdmin);
        }
        else if (spaceDbId.HasValue)
        {
            query = query.Where(ur =>
                ur.SpaceId == spaceDbId.Value
                && ur.RoleId == (int)UserRoleTypeEnum.SpaceMod);
        }
        else if (hubDbId.HasValue)
        {
            query = query.Where(ur =>
                ur.HubId == hubDbId.Value
                && ur.RoleId == (int)UserRoleTypeEnum.HubMod);
        }
        else if (communityDbId.HasValue)
        {
            query = query.Where(ur =>
                ur.CommunityId == communityDbId.Value
                && (ur.RoleId == (int)UserRoleTypeEnum.CommunityAdmin
                    || ur.RoleId == (int)UserRoleTypeEnum.CommunityMod));
        }

        return await query
            .OrderBy(ur => ur.RoleId)
            .ThenBy(ur => ur.AssignedAt)
            .Select(ur => new ModeratorInfoDto
            {
                UserPublicId = ur.User.PublicId,
                DisplayName = ur.User.DisplayName ?? "",
                Role = ((UserRoleTypeEnum)ur.RoleId).ToString(),
                Slug = ur.User.Slug ?? ""
            })
            .ToListAsync(ct);
    }
}
