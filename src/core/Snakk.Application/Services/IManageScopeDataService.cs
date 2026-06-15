using Snakk.Application.DTOs.Management;

namespace Snakk.Application.Services;

/// <summary>
/// Data-access service for management scope resolution and cross-scope hierarchy lookups
/// used by ManageContextEndpoints and ManageGrpcService.
/// </summary>
public interface IManageScopeDataService
{
    /// <summary>
    /// Resolves community by publicId. Returns null if not found.
    /// </summary>
    Task<ScopeEntityDto?> GetCommunityByPublicIdAsync(string publicId, CancellationToken ct = default);

    /// <summary>
    /// Resolves hub by publicId, scoped to a community (by db integer id).
    /// Returns null if not found or does not belong to the community.
    /// </summary>
    Task<ScopeEntityDto?> GetHubByPublicIdInCommunityAsync(string hubPublicId, int communityDbId, CancellationToken ct = default);

    /// <summary>
    /// Resolves space by publicId, scoped to a hub (by db integer id).
    /// Returns null if not found or does not belong to the hub.
    /// </summary>
    Task<ScopeEntityDto?> GetSpaceByPublicIdInHubAsync(string spacePublicId, int hubDbId, CancellationToken ct = default);

    // ===== Scope resolution by slug (used by ManageGrpcService) =====

    Task<ScopeEntityWithAvatarDto?> GetCommunityBySlugAsync(string slug, CancellationToken ct = default);
    Task<ScopeEntityWithAvatarDto?> GetHubBySlugInCommunityAsync(string hubSlug, int communityDbId, CancellationToken ct = default);
    Task<ScopeEntityWithAvatarDto?> GetSpaceBySlugInHubAsync(string spaceSlug, int hubDbId, CancellationToken ct = default);

    // ===== Discord settings (ManageGrpcService) =====

    Task<SpaceDiscordSettingsDto?> GetSpaceDiscordSettingsAsync(string spacePublicId, CancellationToken ct = default);
    Task<bool> UpdateSpaceDiscordSettingsAsync(string spacePublicId, SpaceDiscordSettingsDto settings, CancellationToken ct = default);

    // ===== Global admin check (ManageGrpcService / GetRules / UpdateRules / Site settings) =====

    Task<bool> IsGlobalAdminAsync(string userPublicId, CancellationToken ct = default);

    /// <summary>
    /// Returns the cached set of all active global admin public IDs.
    /// O(1) Contains() check at call site — no per-user DB round-trip.
    /// </summary>
    Task<IReadOnlySet<string>> GetGlobalAdminPublicIdsAsync(CancellationToken ct = default);

    /// <summary>
    /// Invalidates the global admin ID cache. Call after AssignRole / RevokeRole for GlobalAdmin.
    /// </summary>
    Task InvalidateGlobalAdminCacheAsync(CancellationToken ct = default);

    // ===== Parent hierarchy for inherited bans/team/report-reasons =====

    Task<HubParentDto?> GetHubParentAsync(string hubPublicId, CancellationToken ct = default);
    Task<SpaceParentsDto?> GetSpaceParentsAsync(string spacePublicId, CancellationToken ct = default);

    // ===== GetReportReasons / GetBans / GetTeam parent lookups (includes entity objects) =====

    Task<HubWithCommunityDto?> GetHubWithCommunityAsync(string hubPublicId, CancellationToken ct = default);
    Task<SpaceWithHubCommunityDto?> GetSpaceWithHubCommunityAsync(string spacePublicId, CancellationToken ct = default);

    // ===== CreateBan / AddTeamMember user lookup by display name =====

    Task<string?> FindUserPublicIdByDisplayNameAsync(string displayName, CancellationToken ct = default);
    Task<string?> GetUserDisplayNameByPublicIdAsync(string userPublicId, CancellationToken ct = default);

    // ===== ModerateContent: find spacePublicId for a discussion =====

    Task<string?> GetDiscussionSpacePublicIdAsync(string discussionPublicId, CancellationToken ct = default);

    // ===== GetModerators (ModerationGrpcService) =====

    Task<SpaceModeratorSeedDto?> GetSpaceModeratorSeedAsync(string spacePublicId, CancellationToken ct = default);
    Task<HubModeratorSeedDto?> GetHubModeratorSeedAsync(string hubPublicId, CancellationToken ct = default);
    Task<CommunityModeratorSeedDto?> GetCommunityModeratorSeedAsync(string communityPublicId, CancellationToken ct = default);

    /// <summary>
    /// Returns active moderator/admin roles for a given scope id combination.
    /// Exactly one of the id parameters should be set; set globalOnly=true for global admins.
    /// </summary>
    Task<IReadOnlyList<ModeratorInfoDto>> GetActiveModeratorRolesAsync(
        int? communityDbId = null,
        int? hubDbId = null,
        int? spaceDbId = null,
        bool globalOnly = false,
        CancellationToken ct = default);
}
