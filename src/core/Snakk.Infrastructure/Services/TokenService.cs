namespace Snakk.Infrastructure.Services;

using Microsoft.EntityFrameworkCore;
using Snakk.Application.Services;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Shared.Enums;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;

public class TokenService(
    SnakkDbContext context,
    IJwtTokenService jwtTokenService) : ITokenService
{
    public string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);

        return Convert.ToBase64String(randomBytes);
    }

    public async Task<RefreshToken> CreateRefreshTokenAsync(
        UserId userId,
        string deviceName,
        string deviceFingerprint,
        string ipAddress,
        string userAgent,
        int expirationDays = 90)
    {
        var user = await context.Users.FirstOrDefaultAsync(u => u.PublicId == userId.Value);

        if (user is null)
            throw new InvalidOperationException($"User {userId} not found");

        var tokenValue = GenerateRefreshToken();

        var entity = new RefreshTokenDatabaseEntity
        {
            PublicId = Ulid.NewUlid().ToString(),
            TokenValue = tokenValue,
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(expirationDays),
            CreatedAt = DateTime.UtcNow,
            DeviceName = deviceName,
            DeviceFingerprint = deviceFingerprint,
            IpAddress = ipAddress,
            UserAgent = userAgent
        };

        context.RefreshTokens.Add(entity);
        await context.SaveChangesAsync();

        return RefreshToken.Rehydrate(tokenValue, userId, entity.ExpiresAt, entity.CreatedAt, null);
    }

    public async Task<string?> RefreshAccessTokenAsync(string refreshTokenValue, string ipAddress, string? userAgent = null)
    {
        var tokenEntity = await context.RefreshTokens
            .AsTracking()
            .Include(t => t.User)
                .ThenInclude(u => u.Roles.Where(r => r.RevokedAt == null))
            .FirstOrDefaultAsync(t => t.TokenValue == refreshTokenValue);

        if (tokenEntity is null || !tokenEntity.IsActive)
            return null;

        // Device fingerprint validation — reject if UserAgent doesn't match stored value
        if (!string.IsNullOrEmpty(tokenEntity.UserAgent) && !string.IsNullOrEmpty(userAgent)
            && !tokenEntity.UserAgent.Equals(userAgent, StringComparison.Ordinal))
        {
            tokenEntity.RevokedAt = DateTime.UtcNow;
            tokenEntity.RevocationReason = "Device fingerprint mismatch";
            await context.SaveChangesAsync();
            return null;
        }

        tokenEntity.LastUsedAt = DateTime.UtcNow;

        var user = tokenEntity.User;
        var role = user.Roles
            .Where(r => r.RevokedAt == null)
            .Select(r => ((UserRoleTypeEnum)r.RoleId).ToString())
            .FirstOrDefault();

        await context.SaveChangesAsync();

        return jwtTokenService.GenerateToken(
            user.PublicId,
            user.DisplayName,
            user.Email,
            user.EmailVerified,
            user.OAuthProvider,
            role);
    }

    public async Task RevokeRefreshTokenAsync(string tokenValue, string reason)
    {
        var token = await context.RefreshTokens
            .AsTracking()
            .FirstOrDefaultAsync(t => t.TokenValue == tokenValue);

        if (token is not null && !token.IsRevoked)
        {
            token.RevokedAt = DateTime.UtcNow;
            token.RevocationReason = reason;
            await context.SaveChangesAsync();
        }
    }

    public async Task RevokeAllUserTokensAsync(UserId userId, string reason)
    {
        var tokens = await context.RefreshTokens
            .AsTracking()
            .Where(t =>
                t.User.PublicId == userId.Value
                && t.RevokedAt == null)
            .ToListAsync();

        foreach (var token in tokens)
        {
            token.RevokedAt = DateTime.UtcNow;
            token.RevocationReason = reason;
        }

        await context.SaveChangesAsync();
    }

    public DateTime? GetTokenExpiration(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();

            if (!handler.CanReadToken(token))
                return null;

            var jwtToken = handler.ReadJwtToken(token);
            return jwtToken.ValidTo;
        }
        catch
        {
            return null;
        }
    }
}
