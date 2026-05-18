using Snakk.Application.DTOs.Admin;

namespace Snakk.Application.Services;

/// <summary>
/// Service for admin content management operations
/// </summary>
public interface IAdminContentService
{
    Task<ContentOverviewDto> GetContentOverviewAsync(CancellationToken ct = default);
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
    Task<bool> PinDiscussionAsync(string id, string adminUserId, CancellationToken ct = default);
    Task<bool> UnpinDiscussionAsync(string id, string adminUserId, CancellationToken ct = default);
    Task<bool> LockDiscussionAsync(string id, string adminUserId, CancellationToken ct = default);
    Task<bool> UnlockDiscussionAsync(string id, string adminUserId, CancellationToken ct = default);
    Task<bool> DeleteDiscussionAsync(string id, string adminUserId, CancellationToken ct = default);
}
