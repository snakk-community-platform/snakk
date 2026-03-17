using Snakk.Application.DTOs.Management;

namespace Snakk.Application.Services;

public interface ICommunityManagementService
{
    Task<CommunityOverviewDto?> GetOverviewAsync(string communityId, CancellationToken cancellationToken = default);

    Task<CommunitySettingsDto?> GetSettingsAsync(string communityId, CancellationToken cancellationToken = default);

    Task<CommunitySettingsDto?> UpdateSettingsAsync(
        string communityId,
        UpdateCommunitySettingsRequest request,
        CancellationToken cancellationToken = default);

    Task<CommunityModerationDto> GetModerationDataAsync(string communityId, CancellationToken cancellationToken = default);

    Task<CommunityMembersListDto> GetMembersAsync(
        string communityId,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);

    Task<bool> UpdateMemberRoleAsync(
        string communityId,
        string userId,
        UpdateMemberRoleRequest request,
        CancellationToken cancellationToken = default);

    Task<List<HubSpaceItemDto>> GetCommunitySpacesAsync(string communityId, CancellationToken cancellationToken = default);
}
