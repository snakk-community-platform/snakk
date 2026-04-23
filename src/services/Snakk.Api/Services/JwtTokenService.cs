namespace Snakk.Api.Services;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;
using Snakk.Application.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Snakk.Domain.Entities;

public class JwtTokenService(IConfiguration configuration, IMemoryCache memoryCache) : IJwtTokenService
{
    private readonly string _secretKey = configuration["Jwt:SecretKey"]
        ?? throw new InvalidOperationException("JWT SecretKey not configured");
    private readonly string _issuer = configuration["Jwt:Issuer"] ?? "Snakk";
    private readonly string _audience = configuration["Jwt:Audience"] ?? "Snakk";
    private readonly int _expirationMinutes = configuration.GetValue<int>("Jwt:ExpirationMinutes", 15); // 15 minutes default

    private const string RevocationPrefix = "jwt:revoked:";

    public string GenerateToken(string userId, string? displayName, string? email, bool emailVerified, string? oAuthProvider, string? role = null, string? avatarFileName = null, bool needsProfileSetup = false, string? avatarThumbnailFileName = null, string? avatarMicroFileName = null)
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

        if (!string.IsNullOrEmpty(oAuthProvider))
            claims.Add(new("OAuthProvider", oAuthProvider));

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

    public string GenerateToken(User user) =>
        GenerateToken(
            user.PublicId.Value,
            user.DisplayName,
            user.Email,
            user.EmailVerified,
            user.OAuthProvider,
            user.Role,
            user.AvatarFileName,
            user.NeedsProfileSetup,
            user.AvatarThumbnailFileName,
            user.AvatarMicroFileName);

    public ClaimsPrincipal? ValidateToken(string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_secretKey);

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key) { KeyId = "snakk-hmac" },
                ValidateIssuer = true,
                ValidIssuer = _issuer,
                ValidateAudience = true,
                ValidAudience = _audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            var principal = tokenHandler.ValidateToken(token, validationParameters, out _);

            // Check if this token has been revoked
            var jti = principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
            if (jti is not null && memoryCache.TryGetValue(RevocationPrefix + jti, out _))
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

            // Cache the revocation until the token would have expired naturally
            var expiry = jwt.ValidTo - DateTime.UtcNow;
            if (expiry > TimeSpan.Zero)
            {
                memoryCache.Set(RevocationPrefix + jti, true, expiry);
            }
        }
        catch
        {
            // Token couldn't be parsed — nothing to revoke
        }
    }
}
