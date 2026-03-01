using Snakk.Application.DTOs.Management;

namespace Snakk.Application.Services;

public interface ISpaceManagementService
{
    Task<SpaceOverviewDto?> GetOverviewAsync(string spaceId, CancellationToken cancellationToken = default);

    Task<SpaceSettingsDto?> GetSettingsAsync(string spaceId, CancellationToken cancellationToken = default);

    Task<SpaceSettingsDto?> UpdateSettingsAsync(string spaceId, UpdateSpaceSettingsRequest request, CancellationToken cancellationToken = default);

    Task<SpaceModerationDto> GetModerationDataAsync(string spaceId, CancellationToken cancellationToken = default);

    Task<SpaceRulesDto> GetRulesAsync(string spaceId, CancellationToken cancellationToken = default);

    Task<SpaceRulesDto> UpdateRulesAsync(string spaceId, UpdateSpaceRulesRequest request, CancellationToken cancellationToken = default);
}
