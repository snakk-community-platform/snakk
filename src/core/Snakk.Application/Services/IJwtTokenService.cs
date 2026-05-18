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
        string? oAuthProvider,
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
}
