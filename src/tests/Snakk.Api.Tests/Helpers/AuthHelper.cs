using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Snakk.Api.Tests.Helpers;

/// <summary>
/// Helper for generating JWT tokens in integration tests.
/// Uses a known secret key that matches the TestWebServer configuration.
/// </summary>
public static class AuthHelper
{
    /// <summary>
    /// The JWT secret key used in all test configurations.
    /// Must be at least 32 characters for HMAC-SHA256.
    /// </summary>
    public const string TestJwtSecret = "TestSecretKeyForSnakkIntegrationTests!!";

    /// <summary>
    /// The JWT issuer used in all test configurations.
    /// </summary>
    public const string TestJwtIssuer = "Snakk";

    /// <summary>
    /// The JWT audience used in all test configurations.
    /// </summary>
    public const string TestJwtAudience = "Snakk";

    /// <summary>
    /// Generates a valid JWT token for use in authenticated test requests.
    /// </summary>
    /// <param name="userId">The user's public ID claim.</param>
    /// <param name="displayName">The user's display name claim.</param>
    /// <param name="email">The user's email claim (optional).</param>
    /// <param name="emailVerified">Whether the email is verified.</param>
    /// <param name="role">The user's role claim (optional, e.g. "GlobalAdmin").</param>
    /// <returns>A signed JWT token string.</returns>
    public static string GenerateTestToken(
        string userId = "test-user-id",
        string displayName = "Test User",
        string? email = "test@example.com",
        bool emailVerified = true,
        string? role = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name, displayName),
            new("EmailVerified", emailVerified.ToString())
        };

        if (!string.IsNullOrEmpty(email))
            claims.Add(new Claim(ClaimTypes.Email, email));

        if (!string.IsNullOrEmpty(role))
            claims.Add(new Claim(ClaimTypes.Role, role));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestJwtSecret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: TestJwtIssuer,
            audience: TestJwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
