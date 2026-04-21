namespace Snakk.Application.Services;

using Snakk.Application.DTOs.Settings;

public interface ISettingsService
{
    // Generic settings access
    Task<SettingsByCategoryResponse> GetSettingsByCategoryAsync(string category);
    Task<SettingDto?> GetSettingAsync(string category, string key);
    Task<T?> GetSettingValueAsync<T>(string category, string key);
    Task<SettingDto> UpdateSettingAsync(string category, string key, object value, string adminUserId);

    // Category-specific methods
    Task<List<OAuthProviderDto>> GetOAuthProvidersAsync();
    Task UpdateOAuthProviderAsync(string provider, bool enabled, string adminUserId);

    Task<EmailConfigDto> GetEmailConfigAsync();
    Task UpdateEmailConfigAsync(EmailConfigDto config, string adminUserId);

    Task<SiteInfoDto> GetSiteInfoAsync();
    Task UpdateSiteInfoAsync(SiteInfoDto siteInfo, string adminUserId);

    Task<AvatarSettingsDto> GetAvatarSettingsAsync();
    Task UpdateAvatarSettingsAsync(AvatarSettingsDto settings, string adminUserId);

    Task<ContentSettingsDto> GetContentSettingsAsync();
    Task UpdateContentSettingsAsync(ContentSettingsDto settings, string adminUserId);

    Task<RateLimitingSettingsDto> GetRateLimitingSettingsAsync();
    Task UpdateRateLimitingSettingsAsync(RateLimitingSettingsDto settings, string adminUserId);

    Task<RegistrationSettingsDto> GetRegistrationSettingsAsync();
    Task UpdateRegistrationSettingsAsync(RegistrationSettingsDto settings, string adminUserId);
}
