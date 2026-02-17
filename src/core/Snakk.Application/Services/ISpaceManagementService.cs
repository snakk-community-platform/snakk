using Snakk.Application.DTOs.Management;

namespace Snakk.Application.Services;

public interface ISpaceManagementService
{
    Task<SpaceOverviewDto?> GetOverviewAsync(string communitySlug, string spaceSlug, CancellationToken cancellationToken = default);

    Task<SpaceSettingsDto?> GetSettingsAsync(string communitySlug, string spaceSlug, CancellationToken cancellationToken = default);

    Task<SpaceSettingsDto?> UpdateSettingsAsync(string communitySlug, string spaceSlug, UpdateSpaceSettingsRequest request, CancellationToken cancellationToken = default);

    Task<SpaceModerationDto> GetModerationDataAsync(string communitySlug, string spaceSlug, CancellationToken cancellationToken = default);

    Task<SpaceRulesDto> GetRulesAsync(string communitySlug, string spaceSlug, CancellationToken cancellationToken = default);

    Task<SpaceRulesDto> UpdateRulesAsync(string communitySlug, string spaceSlug, UpdateSpaceRulesRequest request, CancellationToken cancellationToken = default);
}
