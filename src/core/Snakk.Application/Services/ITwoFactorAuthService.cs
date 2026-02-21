using Snakk.Application.DTOs.Auth;

namespace Snakk.Application.Services;

/// <summary>
/// Service for two-factor authentication management
/// </summary>
public interface ITwoFactorAuthService
{
    // Setup & Management
    Task<TwoFactorSetupDto> SetupTwoFactorAsync(string userId);
    Task<(bool Success, List<string> BackupCodes, string? Error)> EnableTwoFactorAsync(string userId, string code);
    Task<bool> DisableTwoFactorAsync(string userId, string password);
    Task<TwoFactorStatusDto?> GetTwoFactorStatusAsync(string userId);

    // Verification
    Task<(bool IsValid, bool UsedBackupCode)> VerifyTwoFactorCodeAsync(string userId, string code, string? ipAddress = null);

    // Backup Codes
    Task<BackupCodeStatusDto> GetBackupCodesStatusAsync(string userId);
    Task<List<string>> RegenerateBackupCodesAsync(string userId, string password);

    // Trusted Devices
    Task TrustDeviceAsync(string userId, string deviceFingerprint, string deviceName, string ipAddress, int? expirationDays);
    Task<List<TrustedDeviceDto>> GetTrustedDevicesAsync(string userId);
    Task RevokeDeviceAsync(string deviceId, string reason);
    Task<bool> IsDeviceTrustedAsync(string userId, string deviceFingerprint);
}
