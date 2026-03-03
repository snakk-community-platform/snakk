namespace Snakk.Application.Services;

using Snakk.Domain.ValueObjects;

public interface ITokenService
{
    string GenerateRefreshToken();
    Task<RefreshToken> CreateRefreshTokenAsync(
        UserId userId,
        string deviceName,
        string deviceFingerprint,
        string ipAddress,
        string userAgent,
        int expirationDays = 90);
    Task<string?> RefreshAccessTokenAsync(string refreshTokenValue, string ipAddress);
    Task RevokeRefreshTokenAsync(string tokenValue, string reason);
    Task RevokeAllUserTokensAsync(UserId userId, string reason);
    DateTime? GetTokenExpiration(string token);
}
