namespace Snakk.Application.Services;

using Snakk.Domain.ValueObjects;

public interface ITrustedDeviceService
{
    /// <summary>
    /// Generates a device fingerprint from User-Agent and IP
    /// </summary>
    string GenerateDeviceFingerprint(string userAgent, string ipAddress);

    /// <summary>
    /// Gets a friendly device name from User-Agent
    /// </summary>
    string GetDeviceName(string userAgent);

    /// <summary>
    /// Checks if a device is trusted for a user
    /// </summary>
    Task<bool> IsDeviceTrustedAsync(UserId userId, string deviceFingerprint, CancellationToken ct = default);

    /// <summary>
    /// Trusts a device for a user
    /// </summary>
    Task TrustDeviceAsync(UserId userId, string deviceFingerprint, string deviceName, string ipAddress, int? expirationDays = null, CancellationToken ct = default);

    /// <summary>
    /// Gets all trusted devices for a user
    /// </summary>
    Task<List<TrustedDeviceDto>> GetTrustedDevicesAsync(UserId userId, CancellationToken ct = default);

    /// <summary>
    /// Revokes a trusted device
    /// </summary>
    Task RevokeDeviceAsync(string devicePublicId, string reason, CancellationToken ct = default);

    /// <summary>
    /// Revokes a trusted device only if it belongs to the specified user (IDOR-safe).
    /// Returns false if the device was not found or doesn't belong to the user.
    /// </summary>
    Task<bool> RevokeDeviceForUserAsync(string devicePublicId, string userId, string reason, CancellationToken ct = default);

    /// <summary>
    /// Revokes all trusted devices for a user
    /// </summary>
    Task RevokeAllDevicesAsync(UserId userId, string reason, CancellationToken ct = default);
}

public class TrustedDeviceDto
{
    public required string PublicId { get; set; }
    public required string DeviceName { get; set; }
    public required DateTime TrustedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public string? LastUsedIp { get; set; }
    public bool IsActive { get; set; }
}
