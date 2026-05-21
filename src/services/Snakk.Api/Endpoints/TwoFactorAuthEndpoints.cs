namespace Snakk.Api.Endpoints;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Snakk.Application.Services;
using Snakk.Domain;
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

        group.MapGet("/status", GetTwoFactorStatusAsync)
            .WithName("Get2FAStatus");

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

    private static async Task<IResult> GetTwoFactorStatusAsync(
        HttpContext httpContext,
        ITwoFactorAuthService twoFactorService,
        CancellationToken ct)
    {
        var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim is null) return Results.Unauthorized();

        var status = await twoFactorService.GetTwoFactorStatusAsync(userIdClaim.Value, ct);
        if (status is null)
            return Results.Ok(new { isEnabled = false, unusedBackupCodes = 0 });

        return Results.Ok(new
        {
            status.IsEnabled,
            unusedBackupCodes = status.TotalBackupCodes - status.UsedBackupCodesCount
        });
    }

    private static async Task<IResult> SetupTwoFactorAsync(
        HttpContext httpContext,
        ITwoFactorAuthService twoFactorService,
        CancellationToken ct)
    {
        var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim is null)
            return Results.Unauthorized();

        try
        {
            var setup = await twoFactorService.SetupTwoFactorAsync(userIdClaim.Value, ct);

            return TypedResults.Ok(new TwoFactorSetupResponse(
                setup.Secret,
                setup.QrCodeUrl,
                "Scan the QR code with your authenticator app, then verify with a code to enable 2FA"));
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
        catch (Exception)
        {
            return Results.Problem("An error occurred. Please try again.");
        }
    }

    private static async Task<IResult> EnableTwoFactorAsync(
        [FromBody] EnableTwoFactorRequest request,
        HttpContext httpContext,
        ITwoFactorAuthService twoFactorService,
        CancellationToken ct)
    {
        var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier);

        if (userIdClaim is null)
            return Results.Unauthorized();

        var (success, backupCodes, error) = await twoFactorService.EnableTwoFactorAsync(userIdClaim.Value, request.Code, ct);

        if (!success)
            return Results.BadRequest(new { error });

        return TypedResults.Ok(new TwoFactorEnableResponse(
            "2FA enabled successfully",
            backupCodes,
            "Save these backup codes in a secure place. They can only be used once and will not be shown again."));
    }

    private static async Task<IResult> DisableTwoFactorAsync(
        HttpContext httpContext,
        ITwoFactorAuthService twoFactorService,
        CancellationToken ct)
    {
        var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim is null)
            return Results.Unauthorized();

        var success = await twoFactorService.DisableTwoFactorAsync(userIdClaim.Value, null, ct);

        if (!success)
            return Results.BadRequest(new { error = "2FA is not enabled" });

        return TypedResults.Ok(new MessageResponse("2FA disabled successfully"));
    }

    private static async Task<IResult> VerifyTwoFactorAsync(
        [FromBody] VerifyTwoFactorRequest request,
        ITotpService totpService,
        ITwoFactorSecretProtector secretProtector,
        ITokenService tokenService,
        IJwtTokenService jwtTokenService,
        ITrustedDeviceService trustedDeviceService,
        HttpContext httpContext,
        SnakkDbContext context,
        IMemoryCache cache,
        CancellationToken ct)
    {
        // This endpoint is used during login flow
        // User provides email/password first, gets a temporary token, then verifies 2FA

        var user = await context.Users
            .Include(u => u.TwoFactorBackupCodes)
            .Include(u => u.Roles.Where(r => r.RevokedAt == null))
            .FirstOrDefaultAsync(u => u.Email == request.Email, ct);

        if (user is null || !user.TwoFactorEnabled)
            return Results.BadRequest(new { error = "Invalid request" });

        if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTime.UtcNow)
            return Results.BadRequest(new { error = "Account is temporarily locked due to too many failed login attempts. Please try again later." });

        var isValid = false;

        // Try TOTP code first
        if (!string.IsNullOrEmpty(user.TwoFactorSecret))
        {
            var decryptedSecret = secretProtector.Unprotect(user.TwoFactorSecret);
            if (totpService.VerifyCode(decryptedSecret, request.Code))
            {
                // Reject replayed codes — window is ±1 step (90 s), so TTL matches
                var replayKey = $"2fa_used:{user.PublicId}:{request.Code}";
                if (!cache.TryGetValue(replayKey, out _))
                {
                    cache.Set(replayKey, true, TimeSpan.FromSeconds(90));
                    isValid = true;
                }
            }
        }

        // If TOTP fails, try backup codes
        if (!isValid)
        {
            var unusedBackupCodes = user.TwoFactorBackupCodes
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
                    await context.SaveChangesAsync(ct);

                    isValid = true;
                    break;
                }
            }
        }

        if (!isValid)
        {
            user.FailedLoginAttempts++;
            if (user.FailedLoginAttempts >= 5)
                user.LockoutEnd = DateTime.UtcNow.AddMinutes(15);
            await context.SaveChangesAsync(ct);
            return Results.BadRequest(new { error = "Invalid 2FA code" });
        }

        // Reset lockout counters on successful 2FA
        user.FailedLoginAttempts = 0;
        user.LockoutEnd = null;
        user.LastLoginAt = DateTime.UtcNow;
        await context.SaveChangesAsync(ct);

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
            30); // 30 days — matches standard login path

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
            Expires = DateTimeOffset.UtcNow.AddDays(30)
        });

        var isDeviceTrusted = await trustedDeviceService.IsDeviceTrustedAsync(
            UserId.From(user.PublicId),
            deviceFingerprint);

        return TypedResults.Ok(new TwoFactorVerifyResponse(
            "2FA verified successfully", accessToken, !isDeviceTrusted, deviceFingerprint, deviceName));
    }

    private static async Task<IResult> GetBackupCodesAsync(
        HttpContext httpContext,
        ITwoFactorAuthService twoFactorService,
        CancellationToken ct)
    {
        var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier);

        if (userIdClaim is null)
            return Results.Unauthorized();

        try
        {
            var status = await twoFactorService.GetBackupCodesStatusAsync(userIdClaim.Value, ct);

            return TypedResults.Ok(new BackupCodesStatusResponse(
                status.TotalCount, status.TotalCount - status.UsedCount, status.Codes));
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
        catch (Exception)
        {
            return Results.Problem("An error occurred. Please try again.");
        }
    }

    private static async Task<IResult> RegenerateBackupCodesAsync(
        [FromBody] RegenerateBackupCodesRequest request,
        HttpContext httpContext,
        ITwoFactorAuthService twoFactorService,
        IMemoryCache cache,
        CancellationToken ct)
    {
        var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim is null)
            return Results.Unauthorized();

        if (!MeEndpoints.ValidateSudoToken(request.SudoToken, userIdClaim.Value, cache))
            return Results.Json(new { error = "Authentication required." }, statusCode: 403);

        try
        {
            var backupCodes = await twoFactorService.RegenerateBackupCodesAsync(userIdClaim.Value, null, ct);

            return TypedResults.Ok(new RegenerateBackupCodesResponse(
                "Backup codes regenerated successfully",
                backupCodes,
                "Save these backup codes in a secure place. They can only be used once and will not be shown again."));
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
        catch (Exception)
        {
            return Results.Problem("An error occurred. Please try again.");
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
public record DisableTwoFactorRequest(string SudoToken);
public record SetupTwoFactorRequest(string? SudoToken);
public record VerifyTwoFactorRequest(string Email, string Code);
public record RegenerateBackupCodesRequest(string SudoToken);
public record TrustDeviceRequest(int? ExpirationDays); // 7, 30, 90, or null for never
