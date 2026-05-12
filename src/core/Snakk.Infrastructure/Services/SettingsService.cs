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
                var rawSettings = await _context.SystemSettings
                    .Where(s => s.Category == category)
                    .OrderBy(s => s.Key)
                    .Include(s => s.UpdatedBy)
                    .ToListAsync(cancel);

                var settings = rawSettings.Select(s => new SettingDto
                {
                    Id = s.PublicId,
                    Category = s.Category,
                    Key = s.Key,
                    Value = s.IsEncrypted ? DecryptValue(s.Value) : s.Value,
                    ValueType = s.ValueType,
                    IsEncrypted = s.IsEncrypted,
                    Description = s.Description,
                    UpdatedAt = s.UpdatedAt,
                    UpdatedByUsername = s.UpdatedBy?.DisplayName
                }).ToList();

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
        var s = await LoadCategoryAsync("OAuth");
        return providers.Select(provider => new OAuthProviderDto
        {
            Provider = provider,
            Enabled = GetValue<bool>(s, $"{provider}.Enabled"),
            Configured = IsOAuthProviderConfigured(provider)
        }).ToList();
    }

    public async Task UpdateOAuthProviderAsync(string provider, bool enabled, string adminUserId) =>
        await UpdateSettingAsync("OAuth", $"{provider}.Enabled", enabled, adminUserId);

    public async Task<EmailConfigDto> GetEmailConfigAsync()
    {
        var s = await LoadCategoryAsync("Email");
        return new EmailConfigDto
        {
            Enabled = GetValue<bool>(s, "Enabled"),
            SmtpHost = GetValue<string>(s, "SmtpHost") ?? string.Empty,
            SmtpPort = GetValue<int>(s, "SmtpPort"),
            UseSsl = GetValue<bool>(s, "UseSsl"),
            SmtpUsername = GetValue<string>(s, "SmtpUsername") ?? string.Empty,
            SmtpPassword = GetValue<string>(s, "SmtpPassword") ?? string.Empty,
            FromEmail = GetValue<string>(s, "FromEmail") ?? string.Empty,
            FromName = GetValue<string>(s, "FromName") ?? string.Empty
        };
    }

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
            async cancel =>
            {
                var s = await LoadCategoryAsync("General");
                return new SiteInfoDto
                {
                    SiteName = GetValue<string>(s, "SiteName") ?? "Snakk",
                    SiteDescription = GetValue<string>(s, "SiteDescription") ?? string.Empty,
                    LogoUrl = GetValue<string>(s, "LogoUrl") ?? string.Empty,
                    Timezone = GetValue<string>(s, "Timezone") ?? "UTC",
                    Language = GetValue<string>(s, "Language") ?? "en"
                };
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

    public async Task<AvatarSettingsDto> GetAvatarSettingsAsync()
    {
        var s = await LoadCategoryAsync("Avatar");
        return new AvatarSettingsDto
        {
            DefaultAvatarSize = GetValue<int>(s, "DefaultAvatarSize"),
            MaxUploadSizeMb = GetValue<int>(s, "MaxUploadSizeMb"),
            AllowUploads = GetValue<bool>(s, "AllowUploads"),
            GenerateOnStartup = GetValue<bool>(s, "GenerateOnStartup")
        };
    }

    public async Task UpdateAvatarSettingsAsync(AvatarSettingsDto settings, string adminUserId)
    {
        await UpdateSettingAsync("Avatar", "DefaultAvatarSize", settings.DefaultAvatarSize, adminUserId);
        await UpdateSettingAsync("Avatar", "MaxUploadSizeMb", settings.MaxUploadSizeMb, adminUserId);
        await UpdateSettingAsync("Avatar", "AllowUploads", settings.AllowUploads, adminUserId);
        await UpdateSettingAsync("Avatar", "GenerateOnStartup", settings.GenerateOnStartup, adminUserId);
    }

    public async Task<ContentSettingsDto> GetContentSettingsAsync()
    {
        var s = await LoadCategoryAsync("Content");
        var scripts = ParseScriptGroups(s.GetValueOrDefault("AllowedDisplayNameScripts"));
        return new ContentSettingsDto
        {
            PostMaxLength = GetValue<int>(s, "PostMaxLength"),
            DiscussionTitleMaxLength = GetValue<int>(s, "DiscussionTitleMaxLength"),
            AllowMarkdown = GetValue<bool>(s, "AllowMarkdown"),
            AllowHtml = GetValue<bool>(s, "AllowHtml"),
            RequireModeration = GetValue<bool>(s, "RequireModeration"),
            AllowedDisplayNameScripts = scripts.Select(sg => sg.ToString()).ToList()
        };
    }

    public async Task UpdateContentSettingsAsync(ContentSettingsDto settings, string adminUserId)
    {
        await UpdateSettingAsync("Content", "PostMaxLength", settings.PostMaxLength, adminUserId);
        await UpdateSettingAsync("Content", "DiscussionTitleMaxLength", settings.DiscussionTitleMaxLength, adminUserId);
        await UpdateSettingAsync("Content", "AllowMarkdown", settings.AllowMarkdown, adminUserId);
        await UpdateSettingAsync("Content", "AllowHtml", settings.AllowHtml, adminUserId);
        await UpdateSettingAsync("Content", "RequireModeration", settings.RequireModeration, adminUserId);

        var scripts = (settings.AllowedDisplayNameScripts ?? [])
            .Select(name => Enum.TryParse<ScriptGroup>(name, out var sg) ? (ScriptGroup?)sg : null)
            .Where(sg => sg.HasValue)
            .Select(sg => sg!.Value);
        await UpdateAllowedDisplayNameScriptsAsync(scripts, adminUserId);
    }

    public async Task<IReadOnlyList<ScriptGroup>> GetAllowedDisplayNameScriptsAsync()
    {
        var s = await LoadCategoryAsync("Content");
        return ParseScriptGroups(s.GetValueOrDefault("AllowedDisplayNameScripts"));
    }

    public async Task UpdateAllowedDisplayNameScriptsAsync(IEnumerable<ScriptGroup> scripts, string adminUserId)
    {
        var names = scripts.Select(sg => sg.ToString()).ToList();
        if (names.Count == 0) names = [ScriptGroup.Latin.ToString()];
        var serialized = JsonSerializer.Serialize(names);

        await UpsertRawSettingAsync("Content", "AllowedDisplayNameScripts", serialized, "JSON", adminUserId);
        await _cache.RemoveAsync("settings_category_Content");

        await _securityService.LogAuditAsync(
            action: "UpdateDisplayNameScripts",
            category: "Settings",
            actorUserId: adminUserId,
            targetType: "Setting",
            targetId: "Content.AllowedDisplayNameScripts",
            details: $"Updated allowed display name scripts: {string.Join(", ", names)}",
            severity: AuditLogSeverityEnum.Info);
    }

    public async Task<RateLimitingSettingsDto> GetRateLimitingSettingsAsync()
    {
        var s = await LoadCategoryAsync("RateLimiting");
        return new RateLimitingSettingsDto
        {
            AuthPermitLimit = GetValue<int>(s, "AuthPermitLimit"),
            AuthWindowMinutes = GetValue<int>(s, "AuthWindowMinutes"),
            ApiPermitLimit = GetValue<int>(s, "ApiPermitLimit"),
            ApiWindowMinutes = GetValue<int>(s, "ApiWindowMinutes"),
            ExpensivePermitLimit = GetValue<int>(s, "ExpensivePermitLimit"),
            ExpensiveWindowMinutes = GetValue<int>(s, "ExpensiveWindowMinutes")
        };
    }

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

    public async Task<RegistrationSettingsDto> GetRegistrationSettingsAsync()
    {
        var s = await LoadCategoryAsync("Registration");
        return new RegistrationSettingsDto
        {
            Mode = GetValue<string>(s, "Mode") ?? "Open",
            InviteCode = GetValue<string>(s, "InviteCode") ?? ""
        };
    }

    public async Task UpdateRegistrationSettingsAsync(RegistrationSettingsDto settings, string adminUserId)
    {
        await UpdateSettingAsync("Registration", "Mode", settings.Mode, adminUserId);
        await UpdateSettingAsync("Registration", "InviteCode", settings.InviteCode, adminUserId);
    }

    #endregion

    #region Helper Methods

    private async Task<Dictionary<string, string?>> LoadCategoryAsync(string category)
    {
        var response = await GetSettingsByCategoryAsync(category);
        return response.Settings.ToDictionary(s => s.Key, s => (string?)s.Value);
    }

    private T? GetValue<T>(Dictionary<string, string?> dict, string key)
    {
        var raw = dict.GetValueOrDefault(key);
        return raw is null ? default : DeserializeValue<T>(raw, typeof(T).Name);
    }

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

    private static List<ScriptGroup> ParseScriptGroups(string? raw)
    {
        if (raw is null) return [ScriptGroup.Latin];
        try
        {
            var names = JsonSerializer.Deserialize<List<string>>(raw) ?? [];
            var result = names
                .Select(n => Enum.TryParse<ScriptGroup>(n, out var sg) ? (ScriptGroup?)sg : null)
                .Where(sg => sg.HasValue)
                .Select(sg => sg!.Value)
                .ToList();
            return result.Count > 0 ? result : [ScriptGroup.Latin];
        }
        catch
        {
            return [ScriptGroup.Latin];
        }
    }

    private async Task UpsertRawSettingAsync(
        string category, string key, string serializedValue, string valueType, string adminUserId)
    {
        var existing = await _context.SystemSettings
            .FirstOrDefaultAsync(s => s.Category == category && s.Key == key);

        var user = await _context.Users.FirstOrDefaultAsync(u => u.PublicId == adminUserId);

        if (existing is null)
        {
            _context.SystemSettings.Add(new SystemSettingDatabaseEntity
            {
                PublicId = Ulid.NewUlid().ToString(),
                Category = category,
                Key = key,
                Value = serializedValue,
                ValueType = valueType,
                UpdatedById = user?.Id,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            existing.Value = serializedValue;
            existing.UpdatedById = user?.Id;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
    }

    #endregion
}
