namespace Snakk.Infrastructure.Services;

using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Snakk.Application.DTOs.Settings;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;
using Snakk.Shared.Enums;
using Snakk.Infrastructure.Database.Entities;

public class SettingsService : ISettingsService
{
    private readonly SnakkDbContext _context;
    private readonly HybridCache _cache;
    private readonly IDataProtector _dataProtector;
    private readonly IConfiguration _configuration;
    private readonly ISecurityService _securityService;
    private static readonly HybridCacheEntryOptions CacheOptions = new() { Expiration = TimeSpan.FromMinutes(5) };
    private static readonly HybridCacheEntryOptions LongCacheOptions = new() { Expiration = TimeSpan.FromHours(1) };
    private const string SiteInfoCacheKey = "site-info";

    public SettingsService(
        SnakkDbContext context,
        HybridCache cache,
        IDataProtectionProvider dataProtectionProvider,
        IConfiguration configuration,
        ISecurityService securityService)
    {
        _context = context;
        _cache = cache;
        _dataProtector = dataProtectionProvider.CreateProtector("SettingsEncryption");
        _configuration = configuration;
        _securityService = securityService;
    }

    #region Generic Settings Access

    public async Task<SettingsByCategoryResponse> GetSettingsByCategoryAsync(string category)
    {
        var cacheKey = $"settings_category_{category}";

        return await _cache.GetOrCreateAsync(
            cacheKey,
            async cancel =>
            {
                var settings = await _context.SystemSettings
                    .Where(s => s.Category == category)
                    .OrderBy(s => s.Key)
                    .Select(s => new SettingDto
                    {
                        Id = s.PublicId,
                        Category = s.Category,
                        Key = s.Key,
                        Value = s.IsEncrypted ? DecryptValue(s.Value) : s.Value,
                        ValueType = s.ValueType,
                        IsEncrypted = s.IsEncrypted,
                        Description = s.Description,
                        UpdatedAt = s.UpdatedAt,
                        UpdatedByUsername = s.UpdatedBy != null ? s.UpdatedBy.DisplayName : null
                    })
                    .ToListAsync(cancel);

                return new SettingsByCategoryResponse
                {
                    Category = category,
                    Settings = settings
                };
            },
            CacheOptions);
    }

    public async Task<SettingDto?> GetSettingAsync(string category, string key)
    {
        var setting = await _context.SystemSettings
            .Include(s => s.UpdatedBy)
            .FirstOrDefaultAsync(s =>
                s.Category == category
                && s.Key == key);

        if (setting is null)
            return null;

        return new SettingDto
        {
            Id = setting.PublicId,
            Category = setting.Category,
            Key = setting.Key,
            Value = setting.IsEncrypted ? DecryptValue(setting.Value) : setting.Value,
            ValueType = setting.ValueType,
            IsEncrypted = setting.IsEncrypted,
            Description = setting.Description,
            UpdatedAt = setting.UpdatedAt,
            UpdatedByUsername = setting.UpdatedBy is not null ? setting.UpdatedBy.DisplayName : null
        };
    }

    public async Task<T?> GetSettingValueAsync<T>(string category, string key)
    {
        try
        {
            var setting = await _context.SystemSettings
                .FirstOrDefaultAsync(s =>
                    s.Category == category
                    && s.Key == key);

            if (setting is null)
                return default;

            var value = setting.IsEncrypted ? DecryptValue(setting.Value) : setting.Value;
            return DeserializeValue<T>(value, setting.ValueType);
        }
        catch (Npgsql.PostgresException)
        {
            // Database tables may not exist yet (pre-setup)
            return default;
        }
    }

    public async Task<SettingDto> UpdateSettingAsync(
        string category,
        string key,
        object value,
        string adminUserId)
    {
        var setting = await _context.SystemSettings
            .Include(s => s.UpdatedBy)
            .FirstOrDefaultAsync(s =>
                s.Category == category
                && s.Key == key);

        if (setting is null)
            throw new InvalidOperationException($"Setting {category}.{key} does not exist");

        var user = await _context.Users.FirstOrDefaultAsync(u => u.PublicId == adminUserId);

        if (user is null)
            throw new InvalidOperationException($"User {adminUserId} not found");

        var serializedValue = SerializeValue(value);
        setting.Value = setting.IsEncrypted ? EncryptValue(serializedValue) : serializedValue;
        setting.UpdatedById = user.Id;
        setting.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Invalidate cache
        await _cache.RemoveAsync($"settings_category_{category}");

        // Audit log
        await _securityService.LogAuditAsync(
            action: "UpdateSetting",
            category: "Settings",
            actorUserId: adminUserId,
            targetType: "Setting",
            targetId: setting.PublicId,
            details: $"Updated {category}.{key}",
            severity: AuditLogSeverityEnum.Info);

        return new SettingDto
        {
            Id = setting.PublicId,
            Category = setting.Category,
            Key = setting.Key,
            Value = setting.IsEncrypted ? DecryptValue(setting.Value) : setting.Value,
            ValueType = setting.ValueType,
            IsEncrypted = setting.IsEncrypted,
            Description = setting.Description,
            UpdatedAt = setting.UpdatedAt,
            UpdatedByUsername = setting.UpdatedBy?.DisplayName
        };
    }

    #endregion

    #region Category-Specific Methods

    public async Task<List<OAuthProviderDto>> GetOAuthProvidersAsync()
    {
        var providers = new[] { "Google", "GitHub", "Discord", "Microsoft", "Facebook", "Apple" };
        var result = new List<OAuthProviderDto>();

        foreach (var provider in providers)
        {
            var enabled = await GetSettingValueAsync<bool>("OAuth", $"{provider}.Enabled");
            var configured = IsOAuthProviderConfigured(provider);

            result.Add(new OAuthProviderDto
            {
                Provider = provider,
                Enabled = enabled,
                Configured = configured
            });
        }

        return result;
    }

    public async Task UpdateOAuthProviderAsync(string provider, bool enabled, string adminUserId) =>
        await UpdateSettingAsync("OAuth", $"{provider}.Enabled", enabled, adminUserId);

    public async Task<EmailConfigDto> GetEmailConfigAsync() => new EmailConfigDto
    {
        Enabled = await GetSettingValueAsync<bool>("Email", "Enabled"),
        SmtpHost = await GetSettingValueAsync<string>("Email", "SmtpHost") ?? string.Empty,
        SmtpPort = await GetSettingValueAsync<int>("Email", "SmtpPort"),
        UseSsl = await GetSettingValueAsync<bool>("Email", "UseSsl"),
        SmtpUsername = await GetSettingValueAsync<string>("Email", "SmtpUsername") ?? string.Empty,
        SmtpPassword = await GetSettingValueAsync<string>("Email", "SmtpPassword") ?? string.Empty,
        FromEmail = await GetSettingValueAsync<string>("Email", "FromEmail") ?? string.Empty,
        FromName = await GetSettingValueAsync<string>("Email", "FromName") ?? string.Empty
    };

    public async Task UpdateEmailConfigAsync(EmailConfigDto config, string adminUserId)
    {
        await UpdateSettingAsync("Email", "Enabled", config.Enabled, adminUserId);
        await UpdateSettingAsync("Email", "SmtpHost", config.SmtpHost, adminUserId);
        await UpdateSettingAsync("Email", "SmtpPort", config.SmtpPort, adminUserId);
        await UpdateSettingAsync("Email", "UseSsl", config.UseSsl, adminUserId);
        await UpdateSettingAsync("Email", "SmtpUsername", config.SmtpUsername, adminUserId);
        await UpdateSettingAsync("Email", "SmtpPassword", config.SmtpPassword, adminUserId);
        await UpdateSettingAsync("Email", "FromEmail", config.FromEmail, adminUserId);
        await UpdateSettingAsync("Email", "FromName", config.FromName, adminUserId);
    }

    public async Task<SiteInfoDto> GetSiteInfoAsync() =>
        await _cache.GetOrCreateAsync(
            SiteInfoCacheKey,
            async cancel => new SiteInfoDto
            {
                SiteName = await GetSettingValueAsync<string>("General", "SiteName") ?? "Snakk",
                SiteDescription = await GetSettingValueAsync<string>("General", "SiteDescription") ?? string.Empty,
                LogoUrl = await GetSettingValueAsync<string>("General", "LogoUrl") ?? string.Empty,
                Timezone = await GetSettingValueAsync<string>("General", "Timezone") ?? "UTC",
                Language = await GetSettingValueAsync<string>("General", "Language") ?? "en"
            },
            LongCacheOptions);

    public async Task UpdateSiteInfoAsync(SiteInfoDto siteInfo, string adminUserId)
    {
        await UpdateSettingAsync("General", "SiteName", siteInfo.SiteName, adminUserId);
        await UpdateSettingAsync("General", "SiteDescription", siteInfo.SiteDescription, adminUserId);
        await UpdateSettingAsync("General", "LogoUrl", siteInfo.LogoUrl, adminUserId);
        await UpdateSettingAsync("General", "Timezone", siteInfo.Timezone, adminUserId);
        await UpdateSettingAsync("General", "Language", siteInfo.Language, adminUserId);
        await _cache.RemoveAsync(SiteInfoCacheKey);
    }

    public async Task<AvatarSettingsDto> GetAvatarSettingsAsync() => new AvatarSettingsDto
    {
        DefaultAvatarSize = await GetSettingValueAsync<int>("Avatar", "DefaultAvatarSize"),
        MaxUploadSizeMb = await GetSettingValueAsync<int>("Avatar", "MaxUploadSizeMb"),
        AllowUploads = await GetSettingValueAsync<bool>("Avatar", "AllowUploads"),
        GenerateOnStartup = await GetSettingValueAsync<bool>("Avatar", "GenerateOnStartup")
    };

    public async Task UpdateAvatarSettingsAsync(AvatarSettingsDto settings, string adminUserId)
    {
        await UpdateSettingAsync("Avatar", "DefaultAvatarSize", settings.DefaultAvatarSize, adminUserId);
        await UpdateSettingAsync("Avatar", "MaxUploadSizeMb", settings.MaxUploadSizeMb, adminUserId);
        await UpdateSettingAsync("Avatar", "AllowUploads", settings.AllowUploads, adminUserId);
        await UpdateSettingAsync("Avatar", "GenerateOnStartup", settings.GenerateOnStartup, adminUserId);
    }

    public async Task<ContentSettingsDto> GetContentSettingsAsync() => new ContentSettingsDto
    {
        PostMaxLength = await GetSettingValueAsync<int>("Content", "PostMaxLength"),
        DiscussionTitleMaxLength = await GetSettingValueAsync<int>("Content", "DiscussionTitleMaxLength"),
        AllowMarkdown = await GetSettingValueAsync<bool>("Content", "AllowMarkdown"),
        AllowHtml = await GetSettingValueAsync<bool>("Content", "AllowHtml"),
        RequireModeration = await GetSettingValueAsync<bool>("Content", "RequireModeration")
    };

    public async Task UpdateContentSettingsAsync(ContentSettingsDto settings, string adminUserId)
    {
        await UpdateSettingAsync("Content", "PostMaxLength", settings.PostMaxLength, adminUserId);
        await UpdateSettingAsync("Content", "DiscussionTitleMaxLength", settings.DiscussionTitleMaxLength, adminUserId);
        await UpdateSettingAsync("Content", "AllowMarkdown", settings.AllowMarkdown, adminUserId);
        await UpdateSettingAsync("Content", "AllowHtml", settings.AllowHtml, adminUserId);
        await UpdateSettingAsync("Content", "RequireModeration", settings.RequireModeration, adminUserId);
    }

    public async Task<RateLimitingSettingsDto> GetRateLimitingSettingsAsync() => new RateLimitingSettingsDto
    {
        AuthPermitLimit = await GetSettingValueAsync<int>("RateLimiting", "AuthPermitLimit"),
        AuthWindowMinutes = await GetSettingValueAsync<int>("RateLimiting", "AuthWindowMinutes"),
        ApiPermitLimit = await GetSettingValueAsync<int>("RateLimiting", "ApiPermitLimit"),
        ApiWindowMinutes = await GetSettingValueAsync<int>("RateLimiting", "ApiWindowMinutes"),
        ExpensivePermitLimit = await GetSettingValueAsync<int>("RateLimiting", "ExpensivePermitLimit"),
        ExpensiveWindowMinutes = await GetSettingValueAsync<int>("RateLimiting", "ExpensiveWindowMinutes")
    };

    public async Task UpdateRateLimitingSettingsAsync(RateLimitingSettingsDto settings, string adminUserId)
    {
        await UpdateSettingAsync("RateLimiting", "AuthPermitLimit", settings.AuthPermitLimit, adminUserId);
        await UpdateSettingAsync("RateLimiting", "AuthWindowMinutes", settings.AuthWindowMinutes, adminUserId);
        await UpdateSettingAsync("RateLimiting", "ApiPermitLimit", settings.ApiPermitLimit, adminUserId);
        await UpdateSettingAsync("RateLimiting", "ApiWindowMinutes", settings.ApiWindowMinutes, adminUserId);
        await UpdateSettingAsync("RateLimiting", "ExpensivePermitLimit", settings.ExpensivePermitLimit, adminUserId);
        await UpdateSettingAsync(
            "RateLimiting",
            "ExpensiveWindowMinutes",
            settings.ExpensiveWindowMinutes,
            adminUserId);
    }

    #endregion

    #region Helper Methods

    private string EncryptValue(string value) => _dataProtector.Protect(value);

    private string DecryptValue(string encryptedValue)
    {
        try
        {
            return _dataProtector.Unprotect(encryptedValue);
        }
        catch
        {
            return encryptedValue; // Return as-is if decryption fails
        }
    }

    private string SerializeValue(object value)
    {
        if (value is string str)
            return JsonSerializer.Serialize(str);

        if (value is int || value is long)
            return value.ToString() ?? "0";

        if (value is bool b)
            return b.ToString().ToLower();

        return JsonSerializer.Serialize(value);
    }

    private T? DeserializeValue<T>(string value, string valueType)
    {
        try
        {
            if (typeof(T) == typeof(string))
                return (T)(object)JsonSerializer.Deserialize<string>(value)!;

            if (typeof(T) == typeof(int))
                return (T)(object)int.Parse(value);

            if (typeof(T) == typeof(bool))
                return (T)(object)bool.Parse(value);

            return JsonSerializer.Deserialize<T>(value);
        }
        catch
        {
            return default;
        }
    }

    private bool IsOAuthProviderConfigured(string provider)
    {
        var clientId = _configuration[$"OAuth:{provider}:ClientId"];
        var clientSecret = _configuration[$"OAuth:{provider}:ClientSecret"];

        return !string.IsNullOrEmpty(clientId) && !string.IsNullOrEmpty(clientSecret);
    }

    #endregion
}
