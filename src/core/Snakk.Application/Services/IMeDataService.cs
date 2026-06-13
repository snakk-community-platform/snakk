namespace Snakk.Application.Services;

/// <summary>
/// Data access abstraction for the /me endpoint queries that previously
/// executed directly against SnakkDbContext inside Snakk.Api's MeEndpoints.
/// </summary>
public interface IMeDataService
{
    /// <summary>
    /// Returns the PasswordHash and TwoFactorEnabled flag for the user,
    /// used by the sudo token issuance endpoint to verify credentials before
    /// issuing a short-lived step-up token. Returns null when not found.
    /// </summary>
    Task<UserCredentialData?> GetUserCredentialDataAsync(string userPublicId, CancellationToken ct = default);

    /// <summary>
    /// Returns the PasswordHash for the user, used by VerifyCredential.
    /// Returns null when the user does not exist or has no password set.
    /// </summary>
    Task<string?> GetPasswordHashAsync(string userPublicId, CancellationToken ct = default);

    /// <summary>
    /// Returns the (encrypted) email address for the user, used by
    /// DeleteAccount to compare against the typed confirmation.
    /// Returns null when the user does not exist.
    /// </summary>
    Task<string?> GetEncryptedEmailAsync(string userPublicId, CancellationToken ct = default);

    /// <summary>
    /// Returns the email address for the user, used by BeginSudoPasskey
    /// to initiate a WebAuthn login assertion. Returns null when not found.
    /// </summary>
    Task<string?> GetEmailAsync(string userPublicId, CancellationToken ct = default);

    /// <summary>
    /// Returns the user's active roles (non-revoked), used by UpdateProfile
    /// to include the role in the refreshed JWT. Returns an empty list when
    /// the user has no roles.
    /// </summary>
    Task<List<string>> GetUserRolesAsync(string userPublicId, CancellationToken ct = default);
}

/// <summary>
/// Minimal projection of a UserDatabaseEntity for credential verification.
/// </summary>
public sealed record UserCredentialData(
    string? PasswordHash,
    bool TwoFactorEnabled);
