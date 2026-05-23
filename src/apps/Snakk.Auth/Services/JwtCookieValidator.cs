namespace Snakk.Auth.Services;

using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

/// <summary>
/// Validates JWT cookies (`.Snakk.Auth` / `.Snakk.Auth.Session`) against the shared
/// signing key. Replaces a previous unsigned <c>ReadJwtToken</c> call in
/// <c>OAuth/Challenge.cshtml.cs</c> that accepted any forged JWT and let an attacker
/// drive the OAuth connect flow against the victim's account (CR-21 in
/// docs/SECURITY-AUDIT-2026-05-23.md).
/// </summary>
public interface IJwtCookieValidator
{
    /// <summary>
    /// Validates signature, issuer, audience, and lifetime. Returns the user's
    /// public ID (sub / NameIdentifier claim) on success, <c>null</c> on any failure.
    /// </summary>
    string? ValidateAndExtractUserId(string? jwt);
}

public class JwtCookieValidator : IJwtCookieValidator
{
    private readonly TokenValidationParameters _parameters;
    private readonly JwtSecurityTokenHandler _handler = new();

    public JwtCookieValidator(IConfiguration configuration)
    {
        var secret = configuration["Jwt:SecretKey"]
            ?? throw new InvalidOperationException("Jwt:SecretKey is not configured");

        _parameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
            ValidateIssuer = true,
            ValidIssuer = configuration["Jwt:Issuer"] ?? "Snakk",
            ValidateAudience = true,
            ValidAudience = configuration["Jwt:Audience"] ?? "Snakk",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            RequireSignedTokens = true,
            // Pin the signing algorithm. Without this, an attacker could downgrade
            // to alg=none (or HS256 against an RS256 public key in deployments
            // where the key material is asymmetric). HS256 matches JwtTokenService.
            ValidAlgorithms = ["HS256"]
        };
    }

    public string? ValidateAndExtractUserId(string? jwt)
    {
        if (string.IsNullOrEmpty(jwt)) return null;
        if (!_handler.CanReadToken(jwt)) return null;

        try
        {
            var principal = _handler.ValidateToken(jwt, _parameters, out _);
            return principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        }
        catch
        {
            // Any validation failure (bad signature, alg=none, wrong issuer/audience,
            // expired, malformed) → reject. Treating all failures the same prevents
            // oracle leaks via differential error messages.
            return null;
        }
    }
}
