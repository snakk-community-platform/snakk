using Snakk.Application.DTOs.Management;

namespace Snakk.Application.Services;

public interface IHubManagementService
{
    Task<HubOverviewDto?> GetOverviewAsync(string hubId, CancellationToken cancellationToken = default);

    Task<HubSettingsDto?> GetSettingsAsync(string hubId, CancellationToken cancellationToken = default);

    Task<HubSettingsDto?> UpdateSettingsAsync(string hubId, UpdateHubSettingsRequest request, CancellationToken cancellationToken = default);

    Task<HubModerationDto> GetModerationDataAsync(string hubId, CancellationToken cancellationToken = default);

    Task<HubSpacesDto> GetSpacesAsync(string hubId, CancellationToken cancellationToken = default);

    Task<HubRulesDto> GetRulesAsync(string hubId, CancellationToken cancellationToken = default);

    Task<HubRulesDto> UpdateRulesAsync(string hubId, UpdateHubRulesRequest request, CancellationToken cancellationToken = default);
}
