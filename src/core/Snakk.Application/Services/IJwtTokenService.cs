namespace Snakk.Application.Services;

using System.Security.Claims;
using Snakk.Domain.Entities;

public interface IJwtTokenService
{
    string GenerateToken(
        string userId,
        string? displayName,
        string? email,
        bool emailVerified,
        string? role = null,
        string? avatarFileName = null,
        bool needsProfileSetup = false,
        string? avatarThumbnailFileName = null,
        string? avatarMicroFileName = null,
        long authVersion = 0,
        string? sessionId = null);

    string GenerateToken(User user, string? sessionId = null);

    ClaimsPrincipal? ValidateToken(string token);

    /// <summary>
    /// Revokes a JWT by adding its jti to an in-memory blacklist until the token's natural expiry.
    /// </summary>
    void RevokeToken(string token);

    bool IsRevoked(string jti);

    /// <summary>
    /// Marks a session as revoked. Any access token carrying this session ID is rejected until the cache entry expires.
    /// </summary>
    void RevokeSession(string sessionPublicId);

    /// <summary>
    /// Returns true if the given session public ID has been revoked via
    /// <see cref="RevokeSession"/>. Used by <c>JwtBearerEvents.OnTokenValidated</c>
    /// so REST endpoints honor session revocation — previously the cache entry
    /// was written but no validation path checked it.
    /// </summary>
    bool IsSessionRevoked(string sessionPublicId);

    /// <summary>
    /// Issues a short-lived (5 min) token that proves the bearer has already cleared
    /// the password step of a 2FA login. Bound to the user's public ID; issued for a
    /// distinct JWT audience so the access-token validator rejects it as a session token.
    /// </summary>
    string GenerateTwoFactorPendingToken(string userPublicId);

    /// <summary>
    /// Validates a pending-2FA token issued by <see cref="GenerateTwoFactorPendingToken"/>.
    /// Returns the bound user's public ID on success, <c>null</c> if the token is missing,
    /// malformed, expired, signed by a different key, or lacks the expected audience/purpose.
    /// </summary>
    string? ValidateTwoFactorPendingToken(string token);
}
