namespace Snakk.Application.Services;

using Snakk.Domain.ValueObjects;

public interface ITokenService
{
    string GenerateAccessToken(TokenUser user, List<string> roles);
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

public class TokenUser
{
    public required string PublicId { get; set; }
    public required string DisplayName { get; set; }
    public string? Email { get; set; }
    public bool TwoFactorEnabled { get; set; }
}
