using Snakk.Application.DTOs.Management;

namespace Snakk.Application.Services;

public interface IHubManagementService
{
    Task<HubOverviewDto?> GetOverviewAsync(string communitySlug, string hubSlug, CancellationToken cancellationToken = default);

    Task<HubSettingsDto?> GetSettingsAsync(string communitySlug, string hubSlug, CancellationToken cancellationToken = default);

    Task<HubSettingsDto?> UpdateSettingsAsync(string communitySlug, string hubSlug, UpdateHubSettingsRequest request, CancellationToken cancellationToken = default);

    Task<HubModerationDto> GetModerationDataAsync(string communitySlug, string hubSlug, CancellationToken cancellationToken = default);

    Task<HubSpacesDto> GetSpacesAsync(string communitySlug, string hubSlug, CancellationToken cancellationToken = default);
}
