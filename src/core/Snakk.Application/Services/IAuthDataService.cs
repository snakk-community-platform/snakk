namespace Snakk.Application.Services;

/// <summary>
/// Data access abstraction for auth-related queries that were previously
/// performed directly against SnakkDbContext inside Snakk.Api endpoints and
/// gRPC services. All methods return simple types or Application-layer DTOs —
/// no EF entity types cross the boundary.
/// </summary>
public interface IAuthDataService
{
    /// <summary>
    /// Returns the active (non-revoked) role names for a user, e.g. ["Admin"].
    /// Returns an empty list when the user has no roles.
    /// </summary>
    Task<List<string>> GetUserRolesAsync(string publicId, CancellationToken ct = default);

    /// <summary>
    /// Returns whether the user has two-factor authentication enabled.
    /// Returns false when the user does not exist.
    /// </summary>
    Task<bool> GetTwoFactorEnabledAsync(string publicId, CancellationToken ct = default);

    /// <summary>
    /// Looks up the PublicId of a RefreshToken row by its hashed token value,
    /// and atomically updates the IpAddress and UserAgent columns.
    /// Returns the session PublicId (ULID string), or null if not found.
    /// </summary>
    Task<string?> SetRefreshTokenSessionInfoAsync(string tokenHash, string? ipAddress, string? userAgent, CancellationToken ct = default);

    /// <summary>
    /// Returns the session PublicId for a refresh token identified by its hash.
    /// Used during token refresh to embed the session id in the new JWT.
    /// </summary>
    Task<string?> GetRefreshTokenSessionIdAsync(string tokenHash, CancellationToken ct = default);

    /// <summary>
    /// Returns true if there is an OAuth connection for the given provider + providerUserId.
    /// </summary>
    Task<bool> OAuthConnectionExistsAsync(string provider, string providerUserId, CancellationToken ct = default);

    /// <summary>
    /// Returns a slim projection of the user for passkey login token generation,
    /// looked up by the internal integer UserId returned by the passkey credential lookup.
    /// Returns null when the user does not exist.
    /// </summary>
    Task<PasskeyLoginUserData?> GetPasskeyLoginUserDataAsync(int internalUserId, CancellationToken ct = default);

    /// <summary>
    /// Returns whether the OAuth connection for this user + provider has Require2FA set.
    /// Returns false when the connection does not exist.
    /// </summary>
    Task<bool> GetOAuthConnectionRequire2FAAsync(string userPublicId, string provider, CancellationToken ct = default);

    /// <summary>
    /// Verifies that the OAuth connection identified by provider + providerUserId belongs
    /// to the given user. Returns true when the connection exists and matches.
    /// </summary>
    Task<bool> VerifyOAuthConnectionOwnershipAsync(string userPublicId, string provider, string providerUserId, CancellationToken ct = default);

    // ── Discord mutations ─────────────────────────────────────────────────────

    /// <summary>
    /// Reads the current DiscordUserId/Username/AvatarHash for a user.
    /// Returns null when the user does not exist.
    /// </summary>
    Task<DiscordStatusData?> GetDiscordStatusAsync(string userPublicId, CancellationToken ct = default);

    /// <summary>
    /// Writes a new Discord link token + expiry to the user row.
    /// Returns the token and expiry, or throws when the user is not found.
    /// </summary>
    Task<(string Token, DateTime ExpiresAt)> SetDiscordLinkTokenAsync(string userPublicId, CancellationToken ct = default);

    /// <summary>
    /// Completes the Discord link: finds the user by link token, validates expiry,
    /// checks for duplicate Discord accounts, writes Discord fields and clears the token.
    /// Returns (ResultCode, UserPublicId?) where ResultCode is "OK", "TOKEN_INVALID", or
    /// "ALREADY_LINKED". UserPublicId is non-null only when ResultCode is "OK".
    /// </summary>
    Task<(string ResultCode, string? UserPublicId)> CompleteDiscordLinkAsync(string linkToken, string discordUserId, string discordUsername, string? discordAvatarHash, CancellationToken ct = default);

    /// <summary>
    /// Clears all Discord fields (UserId, Username, AvatarHash, LinkToken, Expiry) for the user.
    /// Throws when the user is not found.
    /// </summary>
    Task UnlinkDiscordAsync(string userPublicId, CancellationToken ct = default);
}

/// <summary>DTO for Discord status data returned from IAuthDataService.</summary>
public sealed record DiscordStatusData(
    string DiscordUserId,
    string? DiscordUsername,
    string? DiscordAvatarHash);

/// <summary>
/// Slim user projection for passkey login — only the fields needed to
/// generate a JWT after a successful WebAuthn assertion.
/// </summary>
public sealed record PasskeyLoginUserData(
    string PublicId,
    string? DisplayName,
    string? Email,
    bool EmailVerified,
    string? AvatarFileName,
    long AuthVersion,
    bool TwoFactorEnabled,
    string? Slug = null);
