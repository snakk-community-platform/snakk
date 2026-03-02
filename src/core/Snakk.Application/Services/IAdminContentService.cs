using Snakk.Application.DTOs.Admin;

namespace Snakk.Application.Services;

/// <summary>
/// Service for admin content management operations
/// </summary>
public interface IAdminContentService
{
    Task<ContentOverviewDto> GetContentOverviewAsync();
    Task<PaginatedResponse<AdminCommunityDto>> GetCommunitiesAsync(int page, int pageSize, string? search);
    Task<PaginatedResponse<AdminHubDto>> GetHubsAsync(int page, int pageSize, string? search, string? communityId);
    Task<PaginatedResponse<AdminSpaceDto>> GetSpacesAsync(int page, int pageSize, string? search, string? hubId);
    Task<PaginatedResponse<AdminDiscussionDto>> GetDiscussionsAsync(
        int page,
        int pageSize,
        string? search,
        string? spaceId,
        bool? isPinned,
        bool? isLocked);
    Task<bool> PinDiscussionAsync(string id, string adminUserId);
    Task<bool> UnpinDiscussionAsync(string id, string adminUserId);
    Task<bool> LockDiscussionAsync(string id, string adminUserId);
    Task<bool> UnlockDiscussionAsync(string id, string adminUserId);
    Task<bool> DeleteDiscussionAsync(string id, string adminUserId);
}
