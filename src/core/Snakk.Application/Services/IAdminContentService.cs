using Snakk.Application.DTOs.Admin;

namespace Snakk.Application.Services;

/// <summary>
/// Service for admin content management operations
/// </summary>
public interface IAdminContentService
{
    // ===== Overview =====

    Task<ContentOverviewDto> GetContentOverviewAsync(CancellationToken ct = default);

    /// <summary>
    /// Extended overview including pinned/locked discussion counts (used by admin REST endpoint).
    /// </summary>
    Task<ContentOverviewExtendedDto> GetContentOverviewExtendedAsync(CancellationToken ct = default);

    // ===== Slug-based paged queries (used by existing slug-based callers) =====

    Task<PaginatedResponse<AdminCommunityDto>> GetCommunitiesAsync(int page, int pageSize, string? search, CancellationToken ct = default);
    Task<PaginatedResponse<AdminHubDto>> GetHubsAsync(int page, int pageSize, string? search, string? communityId, CancellationToken ct = default);
    Task<PaginatedResponse<AdminSpaceDto>> GetSpacesAsync(int page, int pageSize, string? search, string? hubId, CancellationToken ct = default);
    Task<PaginatedResponse<AdminDiscussionDto>> GetDiscussionsAsync(
        int page,
        int pageSize,
        string? search,
        string? spaceId,
        bool? isPinned,
        bool? isLocked,
        CancellationToken ct = default);

    // ===== PublicId-based paged queries (used by admin REST endpoints) =====

    Task<PaginatedResponse<AdminCommunityItemDto>> GetCommunitiesPagedByPublicIdAsync(
        int page, int pageSize, string? search, CancellationToken ct = default);

    Task<AdminCommunityDetailDto?> GetCommunityAsync(string publicId, CancellationToken ct = default);

    Task<PaginatedResponse<AdminHubItemDto>> GetHubsPagedByPublicIdAsync(
        int page, int pageSize, string? search, string? communityPublicId, CancellationToken ct = default);

    Task<PaginatedResponse<AdminSpaceItemDto>> GetSpacesPagedByPublicIdAsync(
        int page, int pageSize, string? search, string? hubPublicId, CancellationToken ct = default);

    Task<PaginatedResponse<AdminDiscussionItemDto>> GetDiscussionsPagedByPublicIdAsync(
        int page,
        int pageSize,
        string? search,
        string? spacePublicId,
        bool? isPinned,
        bool? isLocked,
        CancellationToken ct = default);

    Task<AdminDiscussionDetailDto?> GetDiscussionAsync(string publicId, CancellationToken ct = default);

    // ===== Mutations by publicId (used by admin REST endpoints) =====
    // Each method returns a result code so the endpoint can produce correct HTTP responses.

    Task<AdminDiscussionMutationResult> PinDiscussionByPublicIdAsync(string publicId, string actorPublicId, CancellationToken ct = default);
    Task<AdminDiscussionMutationResult> UnpinDiscussionByPublicIdAsync(string publicId, string actorPublicId, CancellationToken ct = default);
    Task<AdminDiscussionMutationResult> LockDiscussionByPublicIdAsync(string publicId, string actorPublicId, CancellationToken ct = default);
    Task<AdminDiscussionMutationResult> UnlockDiscussionByPublicIdAsync(string publicId, string actorPublicId, CancellationToken ct = default);
    Task<AdminDiscussionMutationResult> SoftDeleteDiscussionByPublicIdAsync(string publicId, string actorPublicId, CancellationToken ct = default);

    // ===== Legacy slug-based mutations (used by existing callers) =====

    Task<bool> PinDiscussionAsync(string id, string adminUserId, CancellationToken ct = default);
    Task<bool> UnpinDiscussionAsync(string id, string adminUserId, CancellationToken ct = default);
    Task<bool> LockDiscussionAsync(string id, string adminUserId, CancellationToken ct = default);
    Task<bool> UnlockDiscussionAsync(string id, string adminUserId, CancellationToken ct = default);
    Task<bool> DeleteDiscussionAsync(string id, string adminUserId, CancellationToken ct = default);
}
