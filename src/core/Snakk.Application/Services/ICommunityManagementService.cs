using Snakk.Application.DTOs.Management;

namespace Snakk.Application.Services;

public interface ICommunityManagementService
{
    Task<CommunityOverviewDto?> GetOverviewAsync(string slug, CancellationToken cancellationToken = default);

    Task<CommunitySettingsDto?> GetSettingsAsync(string slug, CancellationToken cancellationToken = default);

    Task<CommunitySettingsDto?> UpdateSettingsAsync(string slug, UpdateCommunitySettingsRequest request, CancellationToken cancellationToken = default);

    Task<CommunityModerationDto> GetModerationDataAsync(string slug, CancellationToken cancellationToken = default);

    Task<CommunityMembersListDto> GetMembersAsync(string slug, int page = 1, int pageSize = 50, CancellationToken cancellationToken = default);

    Task<bool> UpdateMemberRoleAsync(string slug, string userId, UpdateMemberRoleRequest request, CancellationToken cancellationToken = default);

    Task<List<HubSpaceItemDto>> GetCommunitySpacesAsync(string slug, CancellationToken cancellationToken = default);
}
