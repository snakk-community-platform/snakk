using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Snakk.Application.DTOs.Auth;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;

namespace Snakk.Infrastructure.Services;

public class TwoFactorAuthService : ITwoFactorAuthService
{
    private readonly SnakkDbContext _context;
    private readonly ITotpService _totpService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITrustedDeviceService _trustedDeviceService;
    private readonly ILogger<TwoFactorAuthService> _logger;

    public TwoFactorAuthService(
        SnakkDbContext context,
        ITotpService totpService,
        IPasswordHasher passwordHasher,
        ITrustedDeviceService trustedDeviceService,
        ILogger<TwoFactorAuthService> logger)
    {
        _context = context;
        _totpService = totpService;
        _passwordHasher = passwordHasher;
        _trustedDeviceService = trustedDeviceService;
        _logger = logger;
    }

    public async Task<TwoFactorSetupDto> SetupTwoFactorAsync(string userId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.PublicId == userId);
        if (user == null)
            throw new InvalidOperationException("User not found");

        if (user.TwoFactorEnabled)
            throw new InvalidOperationException("2FA is already enabled");

        // Generate new secret
        var secret = _totpService.GenerateSecret();
        var qrCodeUri = _totpService.GenerateQrCodeUri(
            secret,
            user.Email ?? user.DisplayName,
            "Snakk");

        // Store secret temporarily (will be saved when user enables 2FA)
        user.TwoFactorSecret = secret;
        await _context.SaveChangesAsync();

        _logger.LogInformation("2FA setup initiated for user {UserId}", userId);

        return new TwoFactorSetupDto
        {
            Secret = secret,
            QrCodeUrl = qrCodeUri
        };
    }

    public async Task<(bool Success, List<string> BackupCodes, string? Error)> EnableTwoFactorAsync(string userId, string code)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.PublicId == userId);
        if (user == null)
            return (false, new List<string>(), "User not found");

        if (user.TwoFactorEnabled)
            return (false, new List<string>(), "2FA is already enabled");

        if (string.IsNullOrEmpty(user.TwoFactorSecret))
            return (false, new List<string>(), "2FA setup not initiated. Call /setup first");

        // Verify the code
        if (!_totpService.VerifyCode(user.TwoFactorSecret, code))
            return (false, new List<string>(), "Invalid verification code");

        // Enable 2FA
        user.TwoFactorEnabled = true;
        user.TwoFactorEnabledAt = DateTime.UtcNow;

        // Generate backup codes
        var backupCodes = _totpService.GenerateBackupCodes(10);
        var backupCodeEntities = backupCodes.Select(code => new BackupCodeDatabaseEntity
        {
            PublicId = Ulid.NewUlid().ToString(),
            UserId = user.Id,
            CodeHash = _totpService.HashBackupCode(code),
            CreatedAt = DateTime.UtcNow
        }).ToList();

        _context.BackupCodes.AddRange(backupCodeEntities);
        await _context.SaveChangesAsync();

        _logger.LogInformation("2FA enabled for user {UserId}", userId);

        return (true, backupCodes, null);
    }

    public async Task<bool> DisableTwoFactorAsync(string userId, string password)
    {
        var user = await _context.Users
            .Include(u => u.BackupCodes)
            .Include(u => u.TrustedDevices)
            .FirstOrDefaultAsync(u => u.PublicId == userId);

        if (user == null)
        {
            _logger.LogWarning("User not found for 2FA disable: {UserId}", userId);
            return false;
        }

        if (!user.TwoFactorEnabled)
        {
            _logger.LogWarning("2FA not enabled for user {UserId}", userId);
            return false;
        }

        // Require password confirmation
        if (string.IsNullOrEmpty(user.PasswordHash) ||
            !_passwordHasher.VerifyPassword(password, user.PasswordHash))
        {
            _logger.LogWarning("Invalid password for 2FA disable: {UserId}", userId);
            return false;
        }

        // Disable 2FA
        user.TwoFactorEnabled = false;
        user.TwoFactorSecret = null;
        user.TwoFactorEnabledAt = null;

        // Remove all backup codes
        _context.BackupCodes.RemoveRange(user.BackupCodes);

        // Revoke all trusted devices
        foreach (var device in user.TrustedDevices.Where(d => d.RevokedAt == null))
        {
            device.RevokedAt = DateTime.UtcNow;
            device.RevocationReason = "2FA disabled";
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("2FA disabled for user {UserId}", userId);

        return true;
    }

    public async Task<TwoFactorStatusDto?> GetTwoFactorStatusAsync(string userId)
    {
        var user = await _context.Users
            .Include(u => u.BackupCodes)
            .FirstOrDefaultAsync(u => u.PublicId == userId);

        if (user == null)
            return null;

        return new TwoFactorStatusDto
        {
            IsEnabled = user.TwoFactorEnabled,
            HasBackupCodes = user.BackupCodes.Any(),
            UsedBackupCodesCount = user.BackupCodes.Count(bc => bc.IsUsed),
            TotalBackupCodes = user.BackupCodes.Count
        };
    }

    public async Task<(bool IsValid, bool UsedBackupCode)> VerifyTwoFactorCodeAsync(string userId, string code, string? ipAddress = null)
    {
        var user = await _context.Users
            .Include(u => u.BackupCodes)
            .FirstOrDefaultAsync(u => u.PublicId == userId);

        if (user == null || !user.TwoFactorEnabled)
            return (false, false);

        bool isValid = false;
        bool usedBackupCode = false;

        // Try TOTP code first
        if (!string.IsNullOrEmpty(user.TwoFactorSecret))
        {
            isValid = _totpService.VerifyCode(user.TwoFactorSecret, code);
        }

        // If TOTP fails, try backup codes
        if (!isValid)
        {
            var unusedBackupCodes = user.BackupCodes.Where(bc => !bc.IsUsed).ToList();
            foreach (var backupCode in unusedBackupCodes)
            {
                if (_totpService.VerifyBackupCode(code, backupCode.CodeHash))
                {
                    // Mark backup code as used
                    backupCode.IsUsed = true;
                    backupCode.UsedAt = DateTime.UtcNow;
                    backupCode.UsedIp = ipAddress;
                    await _context.SaveChangesAsync();

                    isValid = true;
                    usedBackupCode = true;
                    _logger.LogInformation("Backup code used for user {UserId}", userId);
                    break;
                }
            }
        }

        return (isValid, usedBackupCode);
    }

    public async Task<BackupCodeStatusDto> GetBackupCodesStatusAsync(string userId)
    {
        var user = await _context.Users
            .Include(u => u.BackupCodes)
            .FirstOrDefaultAsync(u => u.PublicId == userId);

        if (user == null || !user.TwoFactorEnabled)
            throw new InvalidOperationException("2FA is not enabled");

        var codes = user.BackupCodes
            .OrderBy(bc => bc.CreatedAt)
            .Select(bc => bc.PublicId)
            .ToList();

        var usedCount = user.BackupCodes.Count(bc => bc.IsUsed);

        return new BackupCodeStatusDto
        {
            Codes = codes,
            UsedCount = usedCount,
            TotalCount = codes.Count
        };
    }

    public async Task<List<string>> RegenerateBackupCodesAsync(string userId, string password)
    {
        var user = await _context.Users
            .Include(u => u.BackupCodes)
            .FirstOrDefaultAsync(u => u.PublicId == userId);

        if (user == null)
            throw new InvalidOperationException("User not found");

        if (!user.TwoFactorEnabled)
            throw new InvalidOperationException("2FA is not enabled");

        // Require password confirmation
        if (string.IsNullOrEmpty(user.PasswordHash) ||
            !_passwordHasher.VerifyPassword(password, user.PasswordHash))
        {
            throw new InvalidOperationException("Invalid password");
        }

        // Remove old backup codes
        _context.BackupCodes.RemoveRange(user.BackupCodes);

        // Generate new backup codes
        var backupCodes = _totpService.GenerateBackupCodes(10);
        var backupCodeEntities = backupCodes.Select(code => new BackupCodeDatabaseEntity
        {
            PublicId = Ulid.NewUlid().ToString(),
            UserId = user.Id,
            CodeHash = _totpService.HashBackupCode(code),
            CreatedAt = DateTime.UtcNow
        }).ToList();

        _context.BackupCodes.AddRange(backupCodeEntities);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Backup codes regenerated for user {UserId}", userId);

        return backupCodes;
    }

    public async Task TrustDeviceAsync(string userId, string deviceFingerprint, string deviceName, string ipAddress, int? expirationDays)
    {
        var userIdValueObject = Domain.ValueObjects.UserId.From(userId);
        await _trustedDeviceService.TrustDeviceAsync(userIdValueObject, deviceFingerprint, deviceName, ipAddress, expirationDays);
    }

    public async Task<List<Application.Services.TrustedDeviceDto>> GetTrustedDevicesAsync(string userId)
    {
        var userIdValueObject = Domain.ValueObjects.UserId.From(userId);
        return await _trustedDeviceService.GetTrustedDevicesAsync(userIdValueObject);
    }

    public async Task RevokeDeviceAsync(string deviceId, string reason)
    {
        await _trustedDeviceService.RevokeDeviceAsync(deviceId, reason);
    }

    public async Task<bool> IsDeviceTrustedAsync(string userId, string deviceFingerprint)
    {
        var userIdValueObject = Domain.ValueObjects.UserId.From(userId);
        return await _trustedDeviceService.IsDeviceTrustedAsync(userIdValueObject, deviceFingerprint);
    }
}
