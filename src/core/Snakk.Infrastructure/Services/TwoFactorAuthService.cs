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
    public async Task<TwoFactorSetupDto> SetupTwoFactorAsync(string userId)
    {
        var user = await context.Users.AsTracking().FirstOrDefaultAsync(u => u.PublicId == userId);

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
        await context.SaveChangesAsync();

        logger.LogInformation("2FA setup initiated for user {UserId}", userId);

        return new TwoFactorSetupDto
        {
            Secret = secret,
            QrCodeUrl = qrCodeUri
        };
    }

    public async Task<(bool Success, List<string> BackupCodes, string? Error)> EnableTwoFactorAsync(
        string userId,
        string code)
    {
        var user = await context.Users.AsTracking().FirstOrDefaultAsync(u => u.PublicId == userId);

        if (user is null)
            return (false, [], "User not found");

        if (user.TwoFactorEnabled)
            return (false, [], "2FA is already enabled");

        if (string.IsNullOrEmpty(user.TwoFactorSecret))
            return (false, [], "2FA setup not initiated. Call /setup first");

        // Decrypt and verify the code
        var decryptedSecret = secretProtector.Unprotect(user.TwoFactorSecret);
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
        await context.SaveChangesAsync();

        logger.LogInformation("2FA enabled for user {UserId}", userId);

        return (true, backupCodes, null);
    }

    public async Task<bool> DisableTwoFactorAsync(string userId, string password)
    {
        var user = await context.Users
            .AsTracking()
            .Include(u => u.TwoFactorBackupCodes)
            .Include(u => u.TwoFactorTrustedDevices)
            .FirstOrDefaultAsync(u => u.PublicId == userId);

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

        // Require password confirmation
        if (string.IsNullOrEmpty(user.PasswordHash)
            || !passwordHasher.VerifyPassword(password, user.PasswordHash))
        {
            logger.LogWarning("Invalid password for 2FA disable: {UserId}", userId);
            return false;
        }

        // Disable 2FA
        user.TwoFactorEnabled = false;
        user.TwoFactorSecret = null;
        user.TwoFactorEnabledAt = null;

        // Remove all backup codes
        context.TwoFactorBackupCodes.RemoveRange(user.TwoFactorBackupCodes);

        // Revoke all trusted devices
        foreach (var device in user.TwoFactorTrustedDevices.Where(d => d.RevokedAt is null))
        {
            device.RevokedAt = DateTime.UtcNow;
            device.RevocationReason = "2FA disabled";
        }

        await context.SaveChangesAsync();

        logger.LogInformation("2FA disabled for user {UserId}", userId);

        return true;
    }

    public async Task<TwoFactorStatusDto?> GetTwoFactorStatusAsync(string userId)
    {
        var status = await context.Users
            .Where(u => u.PublicId == userId)
            .Select(u => new {
                u.TwoFactorEnabled,
                HasBackupCodes = u.TwoFactorBackupCodes.Any(),
                UsedBackupCodesCount = u.TwoFactorBackupCodes.Count(bc => bc.IsUsed),
                TotalBackupCodes = u.TwoFactorBackupCodes.Count() })
            .FirstOrDefaultAsync();

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
        string? ipAddress = null)
    {
        var user = await context.Users
            .AsTracking()
            .Include(u => u.TwoFactorBackupCodes)
            .FirstOrDefaultAsync(u => u.PublicId == userId);

        if (user is null || !user.TwoFactorEnabled)
            return (false, false);

        var isValid = false;
        var usedBackupCode = false;

        // Try TOTP code first (decrypt the stored secret)
        if (!string.IsNullOrEmpty(user.TwoFactorSecret))
        {
            var decryptedSecret = secretProtector.Unprotect(user.TwoFactorSecret);
            isValid = totpService.VerifyCode(decryptedSecret, code);
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
                    await context.SaveChangesAsync();

                    isValid = true;
                    usedBackupCode = true;
                    logger.LogInformation("Backup code used for user {UserId}", userId);
                    break;
                }
            }
        }

        return (isValid, usedBackupCode);
    }

    public async Task<BackupCodeStatusDto> GetBackupCodesStatusAsync(string userId)
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
            .FirstOrDefaultAsync();

        if (status is null || !status.TwoFactorEnabled)
            throw new DomainException("2FA is not enabled");

        return new BackupCodeStatusDto
        {
            Codes = status.Codes,
            UsedCount = status.UsedCount,
            TotalCount = status.Codes.Count
        };
    }

    public async Task<List<string>> RegenerateBackupCodesAsync(string userId, string password)
    {
        var user = await context.Users
            .AsTracking()
            .Include(u => u.TwoFactorBackupCodes)
            .FirstOrDefaultAsync(u => u.PublicId == userId);

        if (user is null)
            throw new DomainException("User not found");

        if (!user.TwoFactorEnabled)
            throw new DomainException("2FA is not enabled");

        // Require password confirmation
        if (string.IsNullOrEmpty(user.PasswordHash)
            || !passwordHasher.VerifyPassword(password, user.PasswordHash))
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
        await context.SaveChangesAsync();

        logger.LogInformation("Backup codes regenerated for user {UserId}", userId);

        return backupCodes;
    }

    public async Task TrustDeviceAsync(
        string userId,
        string deviceFingerprint,
        string deviceName,
        string ipAddress,
        int? expirationDays)
    {
        var userIdValueObject = Domain.ValueObjects.UserId.From(userId);
        await trustedDeviceService.TrustDeviceAsync(
            userIdValueObject,
            deviceFingerprint,
            deviceName,
            ipAddress,
            expirationDays);
    }

    public async Task<List<Application.Services.TrustedDeviceDto>> GetTrustedDevicesAsync(string userId)
    {
        var userIdValueObject = Domain.ValueObjects.UserId.From(userId);
        return await trustedDeviceService.GetTrustedDevicesAsync(userIdValueObject);
    }

    public async Task RevokeDeviceAsync(string deviceId, string reason) =>
        await trustedDeviceService.RevokeDeviceAsync(deviceId, reason);

    public async Task<bool> IsDeviceTrustedAsync(string userId, string deviceFingerprint)
    {
        var userIdValueObject = Domain.ValueObjects.UserId.From(userId);
        return await trustedDeviceService.IsDeviceTrustedAsync(userIdValueObject, deviceFingerprint);
    }
}
