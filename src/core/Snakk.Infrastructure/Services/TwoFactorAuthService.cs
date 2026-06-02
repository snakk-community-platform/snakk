using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Snakk.Application.DTOs.Auth;
using Snakk.Application.Services;
using Snakk.Domain;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;

namespace Snakk.Infrastructure.Services;

public class TwoFactorAuthService(
    SnakkDbContext context,
    ITotpService totpService,
    IPasswordHasher passwordHasher,
    ITrustedDeviceService trustedDeviceService,
    ITwoFactorSecretProtector secretProtector,
    ILogger<TwoFactorAuthService> logger) : ITwoFactorAuthService
{
    public async Task<TwoFactorSetupDto> SetupTwoFactorAsync(string userId, CancellationToken ct = default)
    {
        var user = await context.Users.AsTracking().FirstOrDefaultAsync(u => u.PublicId == userId, ct);

        if (user is null)
            throw new DomainException("User not found");

        if (user.TwoFactorEnabled)
            throw new DomainException("2FA is already enabled");

        // Generate new secret
        var secret = totpService.GenerateSecret();

        var qrCodeUri = totpService.GenerateQrCodeUri(
            secret,
            user.Email ?? user.DisplayName ?? "",
            "Snakk");

        // Store encrypted secret (will be used when user enables 2FA)
        user.TwoFactorSecret = secretProtector.Protect(secret);
        await context.SaveChangesAsync(ct);

        logger.LogInformation("2FA setup initiated for user {UserId}", userId);

        return new TwoFactorSetupDto
        {
            Secret = secret,
            QrCodeUrl = qrCodeUri
        };
    }

    public async Task<(bool Success, List<string> BackupCodes, string? Error)> EnableTwoFactorAsync(
        string userId,
        string code,
        CancellationToken ct = default)
    {
        var user = await context.Users.AsTracking().FirstOrDefaultAsync(u => u.PublicId == userId, ct);

        if (user is null)
            return (false, [], "User not found");

        if (user.TwoFactorEnabled)
            return (false, [], "2FA is already enabled");

        if (string.IsNullOrEmpty(user.TwoFactorSecret))
            return (false, [], "2FA setup not initiated. Call /setup first");

        // Decrypt and verify the code
        string decryptedSecret;
        try
        {
            decryptedSecret = secretProtector.Unprotect(user.TwoFactorSecret);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to decrypt 2FA secret during enable for user {UserId}", userId);
            return (false, [], "2FA setup has expired. Please restart the setup process.");
        }
        if (!totpService.VerifyCode(decryptedSecret, code))
            return (false, [], "Invalid verification code");

        // Enable 2FA
        user.TwoFactorEnabled = true;
        user.TwoFactorEnabledAt = DateTime.UtcNow;

        // Generate backup codes
        var backupCodes = totpService.GenerateBackupCodes(10);

        var backupCodeEntities = backupCodes
            .Select(code => new TwoFactorBackupCodeDatabaseEntity
            {
                PublicId = Ulid.NewUlid().ToString(),
                UserId = user.Id,
                CodeHash = totpService.HashBackupCode(code),
                CreatedAt = DateTime.UtcNow
            })
            .ToList();

        context.TwoFactorBackupCodes.AddRange(backupCodeEntities);
        await context.SaveChangesAsync(ct);

        logger.LogInformation("2FA enabled for user {UserId}", userId);

        return (true, backupCodes, null);
    }

    public async Task<bool> DisableTwoFactorAsync(string userId, string? password, CancellationToken ct = default)
    {
        var user = await context.Users
            .AsTracking()
            .Include(u => u.TwoFactorBackupCodes)
            .Include(u => u.TwoFactorTrustedDevices)
            .FirstOrDefaultAsync(u => u.PublicId == userId, ct);

        if (user is null)
        {
            logger.LogWarning("User not found for 2FA disable: {UserId}", userId);
            return false;
        }

        if (!user.TwoFactorEnabled)
        {
            logger.LogWarning("2FA not enabled for user {UserId}", userId);
            return false;
        }

        // Password verification is optional; caller must verify TOTP before calling this
        if (!string.IsNullOrEmpty(password))
        {
            if (string.IsNullOrEmpty(user.PasswordHash)
                || !passwordHasher.VerifyPassword(password, user.PasswordHash))
            {
                logger.LogWarning("Invalid password for 2FA disable: {UserId}", userId);
                return false;
            }
        }

        // Disable 2FA
        user.TwoFactorEnabled = false;
        user.TwoFactorSecret = null;
        user.TwoFactorEnabledAt = null;

        // Bump AuthVersion so any existing token with tfa=1 is immediately invalidated
        user.AuthVersion++;
        user.AuthVersionUpdatedAt = DateTime.UtcNow;

        // Remove all backup codes
        context.TwoFactorBackupCodes.RemoveRange(user.TwoFactorBackupCodes);

        // Revoke all trusted devices
        foreach (var device in user.TwoFactorTrustedDevices.Where(d => d.RevokedAt is null))
        {
            device.RevokedAt = DateTime.UtcNow;
            device.RevocationReason = "2FA disabled";
        }

        await context.SaveChangesAsync(ct);

        logger.LogInformation("2FA disabled for user {UserId}", userId);

        return true;
    }

    public async Task<TwoFactorStatusDto?> GetTwoFactorStatusAsync(string userId, CancellationToken ct = default)
    {
        var status = await context.Users
            .Where(u => u.PublicId == userId)
            .Select(u => new {
                u.TwoFactorEnabled,
                HasBackupCodes = u.TwoFactorBackupCodes.Any(),
                UsedBackupCodesCount = u.TwoFactorBackupCodes.Count(bc => bc.IsUsed),
                TotalBackupCodes = u.TwoFactorBackupCodes.Count() })
            .FirstOrDefaultAsync(ct);

        if (status is null)
            return null;

        return new TwoFactorStatusDto
        {
            IsEnabled = status.TwoFactorEnabled,
            HasBackupCodes = status.HasBackupCodes,
            UsedBackupCodesCount = status.UsedBackupCodesCount,
            TotalBackupCodes = status.TotalBackupCodes
        };
    }

    public async Task<(bool IsValid, bool UsedBackupCode)> VerifyTwoFactorCodeAsync(
        string userId,
        string code,
        string? ipAddress = null,
        CancellationToken ct = default)
    {
        var user = await context.Users
            .AsTracking()
            .Include(u => u.TwoFactorBackupCodes)
            .FirstOrDefaultAsync(u => u.PublicId == userId, ct);

        if (user is null || !user.TwoFactorEnabled)
            return (false, false);

        var isValid = false;
        var usedBackupCode = false;

        // Try TOTP code first (decrypt the stored secret)
        if (!string.IsNullOrEmpty(user.TwoFactorSecret))
        {
            try
            {
                var decryptedSecret = secretProtector.Unprotect(user.TwoFactorSecret);
                isValid = totpService.VerifyCode(decryptedSecret, code);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to decrypt 2FA secret for user {UserId}", userId);
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
                if (totpService.VerifyBackupCode(code, backupCode.CodeHash))
                {
                    // Mark backup code as used
                    backupCode.IsUsed = true;
                    backupCode.UsedAt = DateTime.UtcNow;
                    backupCode.UsedIp = ipAddress;
                    await context.SaveChangesAsync(ct);

                    isValid = true;
                    usedBackupCode = true;
                    logger.LogInformation("Backup code used for user {UserId}", userId);
                    break;
                }
            }
        }

        return (isValid, usedBackupCode);
    }

    public async Task<BackupCodeStatusDto> GetBackupCodesStatusAsync(string userId, CancellationToken ct = default)
    {
        var status = await context.Users
            .Where(u => u.PublicId == userId)
            .Select(u => new {
                u.TwoFactorEnabled,
                Codes = u.TwoFactorBackupCodes
                    .OrderBy(bc => bc.CreatedAt)
                    .Select(bc => bc.PublicId)
                    .ToList(),
                UsedCount = u.TwoFactorBackupCodes.Count(bc => bc.IsUsed) })
            .FirstOrDefaultAsync(ct);

        if (status is null || !status.TwoFactorEnabled)
            throw new DomainException("2FA is not enabled");

        return new BackupCodeStatusDto
        {
            Codes = status.Codes,
            UsedCount = status.UsedCount,
            TotalCount = status.Codes.Count
        };
    }

    public async Task<List<string>> RegenerateBackupCodesAsync(string userId, string? password, CancellationToken ct = default)
    {
        var user = await context.Users
            .AsTracking()
            .Include(u => u.TwoFactorBackupCodes)
            .FirstOrDefaultAsync(u => u.PublicId == userId, ct);

        if (user is null)
            throw new DomainException("User not found");

        if (!user.TwoFactorEnabled)
            throw new DomainException("2FA is not enabled");

        // If password is provided, verify it. null means identity was already verified (e.g. via sudo token).
        if (password is not null
            && (string.IsNullOrEmpty(user.PasswordHash)
                || !passwordHasher.VerifyPassword(password, user.PasswordHash)))
            throw new DomainException("Invalid password");

        // Remove old backup codes
        context.TwoFactorBackupCodes.RemoveRange(user.TwoFactorBackupCodes);

        // Generate new backup codes
        var backupCodes = totpService.GenerateBackupCodes(10);

        var backupCodeEntities = backupCodes
            .Select(code => new TwoFactorBackupCodeDatabaseEntity
            {
                PublicId = Ulid.NewUlid().ToString(),
                UserId = user.Id,
                CodeHash = totpService.HashBackupCode(code),
                CreatedAt = DateTime.UtcNow
            })
            .ToList();

        context.TwoFactorBackupCodes.AddRange(backupCodeEntities);
        await context.SaveChangesAsync(ct);

        logger.LogInformation("Backup codes regenerated for user {UserId}", userId);

        return backupCodes;
    }

    public async Task TrustDeviceAsync(
        string userId,
        string deviceFingerprint,
        string deviceName,
        string ipAddress,
        int? expirationDays,
        CancellationToken ct = default)
    {
        var userIdValueObject = Domain.ValueObjects.UserId.From(userId);
        await trustedDeviceService.TrustDeviceAsync(
            userIdValueObject,
            deviceFingerprint,
            deviceName,
            ipAddress,
            expirationDays,
            ct);
    }

    public async Task<List<Application.Services.TrustedDeviceDto>> GetTrustedDevicesAsync(string userId, CancellationToken ct = default)
    {
        var userIdValueObject = Domain.ValueObjects.UserId.From(userId);
        return await trustedDeviceService.GetTrustedDevicesAsync(userIdValueObject, ct);
    }

    public async Task RevokeDeviceAsync(string deviceId, string reason, CancellationToken ct = default) =>
        await trustedDeviceService.RevokeDeviceAsync(deviceId, reason, ct);

    public async Task<bool> IsDeviceTrustedAsync(string userId, string deviceFingerprint, CancellationToken ct = default)
    {
        var userIdValueObject = Domain.ValueObjects.UserId.From(userId);
        return await trustedDeviceService.IsDeviceTrustedAsync(userIdValueObject, deviceFingerprint, ct);
    }
}
