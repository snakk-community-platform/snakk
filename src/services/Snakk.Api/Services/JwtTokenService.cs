namespace Snakk.Api.Services;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.IdentityModel.Tokens;
using Snakk.Application.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Snakk.Domain.Entities;

public class JwtTokenService(IConfiguration configuration, IDistributedCache cache) : IJwtTokenService
{
    private readonly string _secretKey = configuration["Jwt:SecretKey"]
        ?? throw new InvalidOperationException("JWT SecretKey not configured");
    private readonly string _issuer = configuration["Jwt:Issuer"] ?? "Snakk";
    private readonly string _audience = configuration["Jwt:Audience"] ?? "Snakk";
    private readonly int _expirationMinutes = configuration.GetValue<int>("Jwt:ExpirationMinutes", 480);

    private const string RevocationPrefix = "jwt:revoked:";
    private const string SessionRevocationPrefix = "jwt:session-revoked:";

    private const string TwoFactorPendingAudienceSuffix = ":2fa-pending";
    private const string PurposeClaim = "purpose";
    private const string TwoFactorPendingPurpose = "2fa-pending";
    private const int TwoFactorPendingExpirationMinutes = 5;

    private static readonly byte[] Sentinel = [1];

    public string GenerateToken(string userId, string? displayName, string? email, bool emailVerified, string? role = null, string? avatarFileName = null, bool needsProfileSetup = false, string? avatarThumbnailFileName = null, string? avatarMicroFileName = null, long authVersion = 0, string? sessionId = null)
    {
        var jti = Guid.NewGuid().ToString("N");
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Jti, jti),
            new(ClaimTypes.NameIdentifier, userId),
            new("EmailVerified", emailVerified.ToString())
        };

        if (!string.IsNullOrEmpty(displayName))
            claims.Add(new(ClaimTypes.Name, displayName));

        if (!string.IsNullOrEmpty(email))
            claims.Add(new(ClaimTypes.Email, email));

        if (!string.IsNullOrEmpty(role))
            claims.Add(new(ClaimTypes.Role, role));

        if (!string.IsNullOrEmpty(avatarFileName))
            claims.Add(new("AvatarFileName", avatarFileName));

        if (!string.IsNullOrEmpty(avatarThumbnailFileName))
            claims.Add(new("AvatarThumbnailFileName", avatarThumbnailFileName));

        if (!string.IsNullOrEmpty(avatarMicroFileName))
            claims.Add(new("AvatarMicroFileName", avatarMicroFileName));

        if (needsProfileSetup)
            claims.Add(new("NeedsProfileSetup", "true"));

        if (authVersion > 0)
            claims.Add(new(Snakk.Application.Auth.CustomClaimTypes.AuthVersion, authVersion.ToString()));

        if (!string.IsNullOrEmpty(sessionId))
            claims.Add(new(Snakk.Application.Auth.CustomClaimTypes.SessionId, sessionId));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey)) { KeyId = "snakk-hmac" };
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_expirationMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateToken(User user, string? sessionId = null) =>
        GenerateToken(
            user.PublicId.Value,
            user.DisplayName,
            user.Email,
            user.EmailVerified,
            user.Role,
            user.AvatarFileName,
            user.NeedsProfileSetup,
            user.AvatarThumbnailFileName,
            user.AvatarMicroFileName,
            authVersion: user.AuthVersion,
            sessionId: sessionId);

    public ClaimsPrincipal? ValidateToken(string token)
    {
        try
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey)) { KeyId = "snakk-hmac" };

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateIssuer = true,
                ValidIssuer = _issuer,
                ValidateAudience = true,
                ValidAudience = _audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            };

            var handler = new Microsoft.IdentityModel.JsonWebTokens.JsonWebTokenHandler();
            var result = Task.Run(() => handler.ValidateTokenAsync(token, validationParameters)).GetAwaiter().GetResult();

            if (!result.IsValid) return null;

            var principal = new ClaimsPrincipal(result.ClaimsIdentity);

            var jti = principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
            if (jti is not null && cache.Get(RevocationPrefix + jti) is { Length: > 0 })
                return null;

            var sid = principal.FindFirst(Snakk.Application.Auth.CustomClaimTypes.SessionId)?.Value;
            if (sid is not null && cache.Get(SessionRevocationPrefix + sid) is { Length: > 0 })
                return null;

            return principal;
        }
        catch
        {
            return null;
        }
    }

    public void RevokeToken(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            if (handler.ReadToken(token) is not JwtSecurityToken jwt) return;

            var jti = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)?.Value;
            if (jti is null) return;

            var expiry = jwt.ValidTo - DateTime.UtcNow;
            if (expiry > TimeSpan.Zero)
                cache.Set(RevocationPrefix + jti, Sentinel,
                    new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = expiry });
        }
        catch
        {
            // Token couldn't be parsed — nothing to revoke
        }
    }

    public bool IsRevoked(string jti) =>
        cache.Get(RevocationPrefix + jti) is not null;

    public void RevokeSession(string sessionPublicId)
    {
        var ttl = TimeSpan.FromMinutes(_expirationMinutes);
        cache.Set(SessionRevocationPrefix + sessionPublicId, Sentinel,
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl });
    }

    public string GenerateTwoFactorPendingToken(string userPublicId)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(ClaimTypes.NameIdentifier, userPublicId),
            new(PurposeClaim, TwoFactorPendingPurpose)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey)) { KeyId = "snakk-hmac" };
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience + TwoFactorPendingAudienceSuffix,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(TwoFactorPendingExpirationMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string? ValidateTwoFactorPendingToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;

        try
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey)) { KeyId = "snakk-hmac" };

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateIssuer = true,
                ValidIssuer = _issuer,
                ValidateAudience = true,
                ValidAudience = _audience + TwoFactorPendingAudienceSuffix,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            };

            var handler = new Microsoft.IdentityModel.JsonWebTokens.JsonWebTokenHandler();
            var result = Task.Run(() => handler.ValidateTokenAsync(token, validationParameters)).GetAwaiter().GetResult();

            if (!result.IsValid) return null;

            var principal = new ClaimsPrincipal(result.ClaimsIdentity);

            if (principal.FindFirst(PurposeClaim)?.Value != TwoFactorPendingPurpose)
                return null;

            return principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }
        catch
        {
            return null;
        }
    }
}
