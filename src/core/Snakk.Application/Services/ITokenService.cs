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
        int expirationDays = 30,
        CancellationToken ct = default);
    Task<string?> RefreshAccessTokenAsync(string refreshTokenValue, string ipAddress, string? userAgent = null, CancellationToken ct = default);
    Task RevokeRefreshTokenAsync(string tokenValue, string reason, CancellationToken ct = default);
    Task RevokeAllUserTokensAsync(UserId userId, string reason, CancellationToken ct = default);
    DateTime? GetTokenExpiration(string token);
}
