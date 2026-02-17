namespace Snakk.Api.Endpoints;

using Microsoft.AspNetCore.Mvc;
using Snakk.Application.DTOs.Settings;
using Snakk.Application.Services;
using System.Security.Claims;

public static class AdminSettingsEndpoints
{
    public static void MapAdminSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/settings")
            .WithTags("Admin - Settings")
            .RequireAuthorization(policy => policy.RequireRole("Admin"));

        // General Settings
        group.MapGet("/general", GetGeneralSettingsAsync)
            .WithName("AdminGetGeneralSettings");

        group.MapPut("/general", UpdateGeneralSettingsAsync)
            .WithName("AdminUpdateGeneralSettings");

        // OAuth Provider Settings
        group.MapGet("/oauth", GetOAuthProvidersAsync)
            .WithName("AdminGetOAuthProviders");

        group.MapPut("/oauth/{provider}", UpdateOAuthProviderAsync)
            .WithName("AdminUpdateOAuthProvider");

        // Email Settings
        group.MapGet("/email", GetEmailConfigAsync)
            .WithName("AdminGetEmailConfig");

        group.MapPut("/email", UpdateEmailConfigAsync)
            .WithName("AdminUpdateEmailConfig");

        group.MapPost("/email/test", TestEmailConfigAsync)
            .WithName("AdminTestEmailConfig");

        // Avatar Settings
        group.MapGet("/avatar", GetAvatarSettingsAsync)
            .WithName("AdminGetAvatarSettings");

        group.MapPut("/avatar", UpdateAvatarSettingsAsync)
            .WithName("AdminUpdateAvatarSettings");

        // Content Settings
        group.MapGet("/content", GetContentSettingsAsync)
            .WithName("AdminGetContentSettings");

        group.MapPut("/content", UpdateContentSettingsAsync)
            .WithName("AdminUpdateContentSettings");

        // Rate Limiting Settings
        group.MapGet("/rate-limiting", GetRateLimitingSettingsAsync)
            .WithName("AdminGetRateLimitingSettings");

        group.MapPut("/rate-limiting", UpdateRateLimitingSettingsAsync)
            .WithName("AdminUpdateRateLimitingSettings");
    }

    // ==================== General Settings ====================

    private static async Task<IResult> GetGeneralSettingsAsync(
        ISettingsService settingsService)
    {
        var siteInfo = await settingsService.GetSiteInfoAsync();
        return Results.Ok(siteInfo);
    }

    private static async Task<IResult> UpdateGeneralSettingsAsync(
        [FromBody] SiteInfoDto siteInfo,
        ClaimsPrincipal user,
        ISettingsService settingsService,
        ISecurityService securityService)
    {
        var adminUserId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (adminUserId == null)
            return Results.Unauthorized();

        try
        {
            await settingsService.UpdateSiteInfoAsync(siteInfo, adminUserId);

            await securityService.LogAuditAsync(
                action: "UpdateGeneralSettings",
                category: "Settings",
                actorUserId: adminUserId,
                targetType: "Settings",
                targetId: "General",
                details: "Updated general site settings",
                severity: "Info");

            return Results.Ok(siteInfo);
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    // ==================== OAuth Provider Settings ====================

    private static async Task<IResult> GetOAuthProvidersAsync(
        ISettingsService settingsService)
    {
        var providers = await settingsService.GetOAuthProvidersAsync();
        return Results.Ok(new { providers });
    }

    private static async Task<IResult> UpdateOAuthProviderAsync(
        string provider,
        [FromBody] UpdateOAuthProviderRequest request,
        ClaimsPrincipal user,
        ISettingsService settingsService,
        ISecurityService securityService)
    {
        var adminUserId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (adminUserId == null)
            return Results.Unauthorized();

        var validProviders = new[] { "Google", "GitHub", "Discord", "Microsoft", "Facebook", "Apple" };
        if (!validProviders.Contains(provider, StringComparer.OrdinalIgnoreCase))
            return Results.BadRequest(new { error = "Invalid OAuth provider" });

        try
        {
            await settingsService.UpdateOAuthProviderAsync(provider, request.Enabled, adminUserId);

            await securityService.LogAuditAsync(
                action: "UpdateOAuthProvider",
                category: "Settings",
                actorUserId: adminUserId,
                targetType: "OAuthProvider",
                targetId: provider,
                details: $"{provider} OAuth provider {(request.Enabled ? "enabled" : "disabled")}",
                severity: "Info");

            return Results.Ok(new { provider, enabled = request.Enabled });
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    // ==================== Email Settings ====================

    private static async Task<IResult> GetEmailConfigAsync(
        ISettingsService settingsService)
    {
        var config = await settingsService.GetEmailConfigAsync();
        return Results.Ok(config);
    }

    private static async Task<IResult> UpdateEmailConfigAsync(
        [FromBody] EmailConfigDto config,
        ClaimsPrincipal user,
        ISettingsService settingsService,
        ISecurityService securityService)
    {
        var adminUserId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (adminUserId == null)
            return Results.Unauthorized();

        try
        {
            await settingsService.UpdateEmailConfigAsync(config, adminUserId);

            await securityService.LogAuditAsync(
                action: "UpdateEmailConfig",
                category: "Settings",
                actorUserId: adminUserId,
                targetType: "Settings",
                targetId: "Email",
                details: "Updated email configuration",
                severity: "Info");

            return Results.Ok(config);
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> TestEmailConfigAsync(
        [FromBody] TestEmailRequest request,
        IEmailSender emailSender)
    {
        try
        {
            await emailSender.SendEmailAsync(
                request.RecipientEmail,
                "Snakk Email Configuration Test",
                "This is a test email from your Snakk installation. If you received this, your email configuration is working correctly.");

            return Results.Ok(new { message = "Test email sent successfully" });
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = $"Failed to send test email: {ex.Message}" });
        }
    }

    // ==================== Avatar Settings ====================

    private static async Task<IResult> GetAvatarSettingsAsync(
        ISettingsService settingsService)
    {
        var settings = await settingsService.GetAvatarSettingsAsync();
        return Results.Ok(settings);
    }

    private static async Task<IResult> UpdateAvatarSettingsAsync(
        [FromBody] AvatarSettingsDto settings,
        ClaimsPrincipal user,
        ISettingsService settingsService,
        ISecurityService securityService)
    {
        var adminUserId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (adminUserId == null)
            return Results.Unauthorized();

        try
        {
            await settingsService.UpdateAvatarSettingsAsync(settings, adminUserId);

            await securityService.LogAuditAsync(
                action: "UpdateAvatarSettings",
                category: "Settings",
                actorUserId: adminUserId,
                targetType: "Settings",
                targetId: "Avatar",
                details: "Updated avatar settings",
                severity: "Info");

            return Results.Ok(settings);
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    // ==================== Content Settings ====================

    private static async Task<IResult> GetContentSettingsAsync(
        ISettingsService settingsService)
    {
        var settings = await settingsService.GetContentSettingsAsync();
        return Results.Ok(settings);
    }

    private static async Task<IResult> UpdateContentSettingsAsync(
        [FromBody] ContentSettingsDto settings,
        ClaimsPrincipal user,
        ISettingsService settingsService,
        ISecurityService securityService)
    {
        var adminUserId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (adminUserId == null)
            return Results.Unauthorized();

        try
        {
            await settingsService.UpdateContentSettingsAsync(settings, adminUserId);

            await securityService.LogAuditAsync(
                action: "UpdateContentSettings",
                category: "Settings",
                actorUserId: adminUserId,
                targetType: "Settings",
                targetId: "Content",
                details: "Updated content settings",
                severity: "Info");

            return Results.Ok(settings);
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    // ==================== Rate Limiting Settings ====================

    private static async Task<IResult> GetRateLimitingSettingsAsync(
        ISettingsService settingsService)
    {
        var settings = await settingsService.GetRateLimitingSettingsAsync();
        return Results.Ok(settings);
    }

    private static async Task<IResult> UpdateRateLimitingSettingsAsync(
        [FromBody] RateLimitingSettingsDto settings,
        ClaimsPrincipal user,
        ISettingsService settingsService,
        ISecurityService securityService)
    {
        var adminUserId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (adminUserId == null)
            return Results.Unauthorized();

        try
        {
            await settingsService.UpdateRateLimitingSettingsAsync(settings, adminUserId);

            await securityService.LogAuditAsync(
                action: "UpdateRateLimitingSettings",
                category: "Settings",
                actorUserId: adminUserId,
                targetType: "Settings",
                targetId: "RateLimiting",
                details: "Updated rate limiting settings",
                severity: "Warning"); // Warning because it affects security

            return Results.Ok(settings);
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }
}

// Request DTOs
public class UpdateOAuthProviderRequest
{
    public bool Enabled { get; set; }
}
