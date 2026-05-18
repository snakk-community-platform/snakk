namespace Snakk.Application.Services;

using Snakk.Application.DTOs.Settings;
using Snakk.Shared.Enums;

public interface ISettingsService
{
    // Generic settings access
    Task<SettingsByCategoryResponse> GetSettingsByCategoryAsync(string category, CancellationToken ct = default);
    Task<SettingDto?> GetSettingAsync(string category, string key, CancellationToken ct = default);
    Task<T?> GetSettingValueAsync<T>(string category, string key, CancellationToken ct = default);
    Task<SettingDto> UpdateSettingAsync(string category, string key, object value, string adminUserId, CancellationToken ct = default);

    // Category-specific methods
    Task<List<OAuthProviderDto>> GetOAuthProvidersAsync(CancellationToken ct = default);
    Task UpdateOAuthProviderAsync(string provider, bool enabled, string adminUserId, CancellationToken ct = default);

    Task<EmailConfigDto> GetEmailConfigAsync(CancellationToken ct = default);
    Task UpdateEmailConfigAsync(EmailConfigDto config, string adminUserId, CancellationToken ct = default);

    Task<SiteInfoDto> GetSiteInfoAsync(CancellationToken ct = default);
    Task UpdateSiteInfoAsync(SiteInfoDto siteInfo, string adminUserId, CancellationToken ct = default);

    Task<AvatarSettingsDto> GetAvatarSettingsAsync(CancellationToken ct = default);
    Task UpdateAvatarSettingsAsync(AvatarSettingsDto settings, string adminUserId, CancellationToken ct = default);

    Task<ContentSettingsDto> GetContentSettingsAsync(CancellationToken ct = default);
    Task UpdateContentSettingsAsync(ContentSettingsDto settings, string adminUserId, CancellationToken ct = default);

    Task<IReadOnlyList<ScriptGroup>> GetAllowedDisplayNameScriptsAsync(CancellationToken ct = default);
    Task UpdateAllowedDisplayNameScriptsAsync(IEnumerable<ScriptGroup> scripts, string adminUserId, CancellationToken ct = default);

    Task<RateLimitingSettingsDto> GetRateLimitingSettingsAsync(CancellationToken ct = default);
    Task UpdateRateLimitingSettingsAsync(RateLimitingSettingsDto settings, string adminUserId, CancellationToken ct = default);

    Task<RegistrationSettingsDto> GetRegistrationSettingsAsync(CancellationToken ct = default);
    Task UpdateRegistrationSettingsAsync(RegistrationSettingsDto settings, string adminUserId, CancellationToken ct = default);
}
