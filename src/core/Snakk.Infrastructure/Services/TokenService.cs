namespace Snakk.Infrastructure.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Snakk.Application.Services;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Shared.Enums;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

public class TokenService : ITokenService
{
    private readonly SnakkDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly string _secretKey;
    private readonly string _issuer;
    private readonly string _audience;

    public TokenService(SnakkDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
        _secretKey = configuration["Jwt:SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured");
        _issuer = configuration["Jwt:Issuer"] ?? "Snakk";
        _audience = configuration["Jwt:Audience"] ?? "Snakk";
    }

    public string GenerateAccessToken(Application.Services.TokenUser user, List<string> roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.PublicId),
            new(ClaimTypes.Name, user.DisplayName),
            new("2fa_enabled", user.TwoFactorEnabled.ToString())
        };

        if (!string.IsNullOrEmpty(user.Email))
            claims.Add(new(ClaimTypes.Email, user.Email));

        foreach (var role in roles)
        {
            claims.Add(new(ClaimTypes.Role, role));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

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
        var user = await _context.Users.FirstOrDefaultAsync(u => u.PublicId == userId.Value);

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

        _context.RefreshTokens.Add(entity);
        await _context.SaveChangesAsync();

        return RefreshToken.Rehydrate(tokenValue, userId, entity.ExpiresAt, entity.CreatedAt, null);
    }

    public async Task<string?> RefreshAccessTokenAsync(string refreshTokenValue, string ipAddress)
    {
        var tokenEntity = await _context.RefreshTokens
            .AsTracking()
            .Include(t => t.User)
                .ThenInclude(u => u.Roles.Where(r => r.RevokedAt == null))
            .FirstOrDefaultAsync(t => t.TokenValue == refreshTokenValue);

        if (tokenEntity is null || !tokenEntity.IsActive)
            return null;

        // Update last used
        tokenEntity.LastUsedAt = DateTime.UtcNow;

        // Get user roles
        var roles = tokenEntity.User.Roles
            .Where(r => r.RevokedAt == null)
            .Select(r => ((UserRoleTypeEnum)r.RoleId).ToString())
            .ToList();

        var user = new Application.Services.TokenUser
        {
            PublicId = tokenEntity.User.PublicId,
            DisplayName = tokenEntity.User.DisplayName,
            Email = tokenEntity.User.Email,
            TwoFactorEnabled = tokenEntity.User.TwoFactorEnabled
        };

        await _context.SaveChangesAsync();

        return GenerateAccessToken(user, roles);
    }

    public async Task RevokeRefreshTokenAsync(string tokenValue, string reason)
    {
        var token = await _context.RefreshTokens
            .AsTracking()
            .FirstOrDefaultAsync(t => t.TokenValue == tokenValue);

        if (token is not null && !token.IsRevoked)
        {
            token.RevokedAt = DateTime.UtcNow;
            token.RevocationReason = reason;
            await _context.SaveChangesAsync();
        }
    }

    public async Task RevokeAllUserTokensAsync(UserId userId, string reason)
    {
        var tokens = await _context.RefreshTokens
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

        await _context.SaveChangesAsync();
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
