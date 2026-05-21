using Snakk.Application.DTOs.Auth;

namespace Snakk.Application.Services;

/// <summary>
/// Service for two-factor authentication management
/// </summary>
public interface ITwoFactorAuthService
{
    // Setup & Management
    Task<TwoFactorSetupDto> SetupTwoFactorAsync(string userId, CancellationToken ct = default);
    Task<(bool Success, List<string> BackupCodes, string? Error)> EnableTwoFactorAsync(string userId, string code, CancellationToken ct = default);
    Task<bool> DisableTwoFactorAsync(string userId, string? password, CancellationToken ct = default);
    Task<TwoFactorStatusDto?> GetTwoFactorStatusAsync(string userId, CancellationToken ct = default);

    // Verification
    Task<(bool IsValid, bool UsedBackupCode)> VerifyTwoFactorCodeAsync(string userId, string code, string? ipAddress = null, CancellationToken ct = default);

    // Backup Codes
    Task<BackupCodeStatusDto> GetBackupCodesStatusAsync(string userId, CancellationToken ct = default);
    Task<List<string>> RegenerateBackupCodesAsync(string userId, string? password, CancellationToken ct = default);

    // Trusted Devices
    Task TrustDeviceAsync(string userId, string deviceFingerprint, string deviceName, string ipAddress, int? expirationDays, CancellationToken ct = default);
    Task<List<TrustedDeviceDto>> GetTrustedDevicesAsync(string userId, CancellationToken ct = default);
    Task RevokeDeviceAsync(string deviceId, string reason, CancellationToken ct = default);
    Task<bool> IsDeviceTrustedAsync(string userId, string deviceFingerprint, CancellationToken ct = default);
}
