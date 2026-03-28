namespace Snakk.Api.Endpoints;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Snakk.Application.Services;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database;
using Snakk.Application.DTOs.Responses;
using System.Security.Claims;

public static class TwoFactorAuthEndpoints
{
    public static void MapTwoFactorAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth/2fa")
            .WithTags("Two-Factor Authentication")
            .RequireAuthorization();

        group.MapPost("/setup", SetupTwoFactorAsync)
            .WithName("Setup2FA")
            .Produces<TwoFactorSetupResponse>();

        group.MapPost("/enable", EnableTwoFactorAsync)
            .WithName("Enable2FA")
            .Produces<TwoFactorEnableResponse>();

        group.MapPost("/disable", DisableTwoFactorAsync)
            .WithName("Disable2FA")
            .Produces<MessageResponse>();

        group.MapPost("/verify", VerifyTwoFactorAsync)
            .WithName("Verify2FA")
            .Produces<TwoFactorVerifyResponse>()
            .AllowAnonymous() // Needed during login flow
            .RequireRateLimiting("auth");

        group.MapGet("/backup-codes", GetBackupCodesAsync)
            .WithName("GetBackupCodes")
            .Produces<BackupCodesStatusResponse>();

        group.MapPost("/backup-codes/regenerate", RegenerateBackupCodesAsync)
            .WithName("RegenerateBackupCodes")
            .Produces<RegenerateBackupCodesResponse>();

        group.MapPost("/trust-device", TrustDeviceAsync)
            .WithName("TrustDevice")
            .Produces<TrustDeviceResponse>();

        group.MapGet("/trusted-devices", GetTrustedDevicesAsync)
            .WithName("GetTrustedDevices");

        group.MapDelete("/trusted-devices/{deviceId}", RevokeTrustedDeviceAsync)
            .WithName("RevokeTrustedDevice")
            .Produces<MessageResponse>();
    }

    private static async Task<IResult> SetupTwoFactorAsync(
        HttpContext httpContext,
        ITwoFactorAuthService twoFactorService)
    {
        var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier);

        if (userIdClaim is null)
            return Results.Unauthorized();

        try
        {
            var setup = await twoFactorService.SetupTwoFactorAsync(userIdClaim.Value);

            return TypedResults.Ok(new TwoFactorSetupResponse(
                setup.Secret,
                setup.QrCodeUrl,
                "Scan the QR code with your authenticator app, then verify with a code to enable 2FA"));
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> EnableTwoFactorAsync(
        [FromBody] EnableTwoFactorRequest request,
        HttpContext httpContext,
        ITwoFactorAuthService twoFactorService)
    {
        var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier);

        if (userIdClaim is null)
            return Results.Unauthorized();

        var (success, backupCodes, error) = await twoFactorService.EnableTwoFactorAsync(userIdClaim.Value, request.Code);

        if (!success)
            return Results.BadRequest(new { error });

        return TypedResults.Ok(new TwoFactorEnableResponse(
            "2FA enabled successfully",
            backupCodes,
            "Save these backup codes in a secure place. They can only be used once and will not be shown again."));
    }

    private static async Task<IResult> DisableTwoFactorAsync(
        [FromBody] DisableTwoFactorRequest request,
        HttpContext httpContext,
        ITwoFactorAuthService twoFactorService)
    {
        var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier);

        if (userIdClaim is null)
            return Results.Unauthorized();

        // Verify TOTP code first (prevents account takeover via session hijacking)
        if (string.IsNullOrWhiteSpace(request.TotpCode))
            return Results.BadRequest(new { error = "A valid 2FA code is required to disable 2FA" });

        var (isValid, _) = await twoFactorService.VerifyTwoFactorCodeAsync(userIdClaim.Value, request.TotpCode);
        if (!isValid)
            return Results.BadRequest(new { error = "Invalid 2FA code" });

        var success = await twoFactorService.DisableTwoFactorAsync(userIdClaim.Value, request.Password);

        if (!success)
            return Results.BadRequest(new { error = "Invalid password or 2FA not enabled" });

        return TypedResults.Ok(new MessageResponse("2FA disabled successfully"));
    }

    private static async Task<IResult> VerifyTwoFactorAsync(
        [FromBody] VerifyTwoFactorRequest request,
        ITotpService totpService,
        ITokenService tokenService,
        IJwtTokenService jwtTokenService,
        ITrustedDeviceService trustedDeviceService,
        HttpContext httpContext,
        SnakkDbContext context)
    {
        // This endpoint is used during login flow
        // User provides email/password first, gets a temporary token, then verifies 2FA

        var user = await context.Users
            .Include(u => u.BackupCodes)
            .Include(u => u.Roles.Where(r => r.RevokedAt == null))
            .FirstOrDefaultAsync(u => u.Email == request.Email);

        if (user is null || !user.TwoFactorEnabled)
            return Results.BadRequest(new { error = "Invalid request" });

        var isValid = false;

        // Try TOTP code first
        if (!string.IsNullOrEmpty(user.TwoFactorSecret))
        {
            isValid = totpService.VerifyCode(user.TwoFactorSecret, request.Code);
        }

        // If TOTP fails, try backup codes
        if (!isValid)
        {
            var unusedBackupCodes = user.BackupCodes
                .Where(bc => !bc.IsUsed)
                .ToList();

            foreach (var backupCode in unusedBackupCodes)
            {
                if (totpService.VerifyBackupCode(request.Code, backupCode.CodeHash))
                {
                    // Mark backup code as used
                    backupCode.IsUsed = true;
                    backupCode.UsedAt = DateTime.UtcNow;
                    backupCode.UsedIp = httpContext.Connection.RemoteIpAddress?.ToString();
                    await context.SaveChangesAsync();

                    isValid = true;
                    break;
                }
            }
        }

        if (!isValid)
            return Results.BadRequest(new { error = "Invalid 2FA code" });

        // Generate tokens with 2FA verified claim
        var role = user.Roles
            .Select(r => ((Snakk.Shared.Enums.UserRoleTypeEnum)r.RoleId).ToString())
            .FirstOrDefault();

        var accessToken = jwtTokenService.GenerateToken(
            user.PublicId,
            user.DisplayName,
            user.Email,
            user.EmailVerified,
            user.OAuthProvider,
            role);

        // Check if device should be trusted
        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var userAgent = httpContext.Request.Headers.UserAgent.ToString();
        var deviceFingerprint = trustedDeviceService.GenerateDeviceFingerprint(userAgent, ipAddress);
        var deviceName = trustedDeviceService.GetDeviceName(userAgent);

        // Create refresh token with device info
        var refreshToken = await tokenService.CreateRefreshTokenAsync(
            UserId.From(user.PublicId),
            deviceName,
            deviceFingerprint,
            ipAddress,
            userAgent,
            90); // 90 days

        // Set tokens in cookies
        httpContext.Response.Cookies.Append("access_token", accessToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddMinutes(30)
        });

        httpContext.Response.Cookies.Append("refresh_token", refreshToken.Value, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddDays(90)
        });

        var isDeviceTrusted = await trustedDeviceService.IsDeviceTrustedAsync(
            UserId.From(user.PublicId),
            deviceFingerprint);

        return TypedResults.Ok(new TwoFactorVerifyResponse(
            "2FA verified successfully", accessToken, !isDeviceTrusted, deviceFingerprint, deviceName));
    }

    private static async Task<IResult> GetBackupCodesAsync(
        HttpContext httpContext,
        ITwoFactorAuthService twoFactorService)
    {
        var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier);

        if (userIdClaim is null)
            return Results.Unauthorized();

        try
        {
            var status = await twoFactorService.GetBackupCodesStatusAsync(userIdClaim.Value);

            return TypedResults.Ok(new BackupCodesStatusResponse(
                status.TotalCount, status.TotalCount - status.UsedCount, status.Codes));
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> RegenerateBackupCodesAsync(
        [FromBody] RegenerateBackupCodesRequest request,
        HttpContext httpContext,
        ITwoFactorAuthService twoFactorService)
    {
        var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier);

        if (userIdClaim is null)
            return Results.Unauthorized();

        try
        {
            var backupCodes = await twoFactorService.RegenerateBackupCodesAsync(userIdClaim.Value, request.Password);

            return TypedResults.Ok(new RegenerateBackupCodesResponse(
                "Backup codes regenerated successfully",
                backupCodes,
                "Save these backup codes in a secure place. They can only be used once and will not be shown again."));
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> TrustDeviceAsync(
        [FromBody] TrustDeviceRequest request,
        HttpContext httpContext,
        ITrustedDeviceService trustedDeviceService)
    {
        var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier);

        if (userIdClaim is null)
            return Results.Unauthorized();

        var userId = UserId.From(userIdClaim.Value);
        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var userAgent = httpContext.Request.Headers.UserAgent.ToString();
        var deviceFingerprint = trustedDeviceService.GenerateDeviceFingerprint(userAgent, ipAddress);
        var deviceName = trustedDeviceService.GetDeviceName(userAgent);

        // Trust device with user-selected expiration
        int? expirationDays = request.ExpirationDays switch
        {
            7 => 7,
            30 => 30,
            90 => 90,
            _ => null // Never expire
        };

        await trustedDeviceService.TrustDeviceAsync(userId, deviceFingerprint, deviceName, ipAddress, expirationDays);

        return TypedResults.Ok(new TrustDeviceResponse(
            "Device trusted successfully",
            expirationDays.HasValue ? $"{expirationDays} days" : "never"));
    }

    private static async Task<IResult> GetTrustedDevicesAsync(
        HttpContext httpContext,
        ITrustedDeviceService trustedDeviceService)
    {
        var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier);

        if (userIdClaim is null)
            return Results.Unauthorized();

        var userId = UserId.From(userIdClaim.Value);
        var devices = await trustedDeviceService.GetTrustedDevicesAsync(userId);

        return TypedResults.Ok(devices);
    }

    private static async Task<IResult> RevokeTrustedDeviceAsync(
        string deviceId,
        HttpContext httpContext,
        ITrustedDeviceService trustedDeviceService)
    {
        var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier);

        if (userIdClaim is null)
            return Results.Unauthorized();

        // Verify the device belongs to this user before revoking (IDOR prevention)
        var success = await trustedDeviceService.RevokeDeviceForUserAsync(deviceId, userIdClaim.Value, "User requested revocation");
        if (!success)
            return Results.NotFound();

        return TypedResults.Ok(new MessageResponse("Device trust revoked successfully"));
    }
}

// Request DTOs
public record EnableTwoFactorRequest(string Code);
public record DisableTwoFactorRequest(string Password, string TotpCode);
public record VerifyTwoFactorRequest(string Email, string Code);
public record RegenerateBackupCodesRequest(string Password);
public record TrustDeviceRequest(int? ExpirationDays); // 7, 30, 90, or null for never
