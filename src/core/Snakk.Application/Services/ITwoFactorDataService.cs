namespace Snakk.Application.Services;

/// <summary>
/// Data access abstraction for the two-factor login verification flow that
/// previously executed directly against SnakkDbContext inside Snakk.Api
/// endpoints and gRPC services.
///
/// The entire AsTracking query + backup-code consumption + lockout mutation +
/// SaveChanges sequence is encapsulated here so the caller keeps only HTTP/gRPC
/// shaping and token-issuance orchestration.
/// </summary>
public interface ITwoFactorDataService
{
    /// <summary>
    /// Loads the user (with backup codes and active roles) identified by
    /// <paramref name="userPublicId"/> for the REST VerifyTwoFactor endpoint.
    /// Returns null when not found or 2FA is not enabled.
    /// </summary>
    Task<TwoFactorUserData?> GetUserForTwoFactorVerifyAsync(string userPublicId, CancellationToken ct = default);

    /// <summary>
    /// Loads the user (with backup codes and active roles) identified by
    /// <paramref name="emailHash"/> for the gRPC VerifyTwoFactorLogin RPC.
    /// Returns null when not found or 2FA is not enabled.
    /// </summary>
    Task<TwoFactorUserData?> GetUserForTwoFactorLoginByEmailHashAsync(string emailHash, CancellationToken ct = default);

    /// <summary>
    /// Marks the backup code <paramref name="backupCodeId"/> as used and
    /// records the UsedAt timestamp and optional IP address.
    /// Also calls SaveChangesAsync — the tracking context is owned by the service.
    /// </summary>
    Task ConsumeBackupCodeAsync(int backupCodeId, string? usedIp, CancellationToken ct = default);

    /// <summary>
    /// Increments the FailedLoginAttempts counter on the user and, if the
    /// counter reaches or exceeds 5, sets a 15-minute LockoutEnd.
    /// Calls SaveChangesAsync.
    /// </summary>
    Task IncrementFailedLoginAttemptsAsync(string userPublicId, CancellationToken ct = default);

    /// <summary>
    /// Resets FailedLoginAttempts to 0, clears LockoutEnd, and sets
    /// LastLoginAt to UtcNow. Calls SaveChangesAsync.
    /// </summary>
    Task RecordSuccessfulLoginAsync(string userPublicId, CancellationToken ct = default);
}

/// <summary>
/// Projection of a UserDatabaseEntity used by the two-factor verification flow.
/// Contains only the fields needed for 2FA decisions and token generation.
/// </summary>
public sealed class TwoFactorUserData
{
    public required string PublicId { get; init; }
    public required string? DisplayName { get; init; }
    public required string? Email { get; init; }
    public required bool EmailVerified { get; init; }
    public required string? AvatarFileName { get; init; }
    public required long AuthVersion { get; init; }
    public required bool TwoFactorEnabled { get; init; }
    public required string? TwoFactorSecret { get; init; }
    public required DateTime? LockoutEnd { get; init; }
    public required List<TwoFactorBackupCodeData> BackupCodes { get; init; }
    public required List<string> Roles { get; init; }
}

/// <summary>
/// Minimal projection of a TwoFactorBackupCode database row.
/// </summary>
public sealed class TwoFactorBackupCodeData
{
    public required int Id { get; init; }
    public required bool IsUsed { get; init; }
    public required string CodeHash { get; init; }
}
