using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Snakk.Web.Tests.Helpers;

/// <summary>
/// Generates JWT tokens for use in integration tests.
/// The secret key matches what TestWebApp configures for the test server.
/// </summary>
public static class TestJwtHelper
{
    /// <summary>
    /// Must match the key set via builder.UseSetting("Jwt:SecretKey", ...) in TestWebApp.
    /// At least 32 characters long for HMAC-SHA256.
    /// </summary>
    public const string TestSecretKey = "TestSecretKeyForSnakkWebTests_AtLeast32CharsLong!!";

    /// <summary>
    /// Generate a valid JWT token for test requests.
    /// </summary>
    public static string GenerateToken(
        string userId = "test-user-001",
        string displayName = "Test User",
        string email = "test@snakk.test",
        string role = "User")
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name, displayName),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Role, role),
            new("2fa_enabled", "False")
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestSecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: "Snakk",
            audience: "Snakk",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Generate an expired JWT token (for testing refresh flows).
    /// </summary>
    public static string GenerateExpiredToken(
        string userId = "test-user-001",
        string displayName = "Test User")
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name, displayName),
            new("2fa_enabled", "False")
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestSecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: "Snakk",
            audience: "Snakk",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(-5), // Already expired
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Create an HttpClient from the factory with auth cookie set.
    /// </summary>
    public static HttpClient CreateAuthenticatedClient(
        TestWebApp app,
        string userId = "test-user-001",
        string displayName = "Test User",
        string role = "User")
    {
        var token = GenerateToken(userId, displayName, role: role);
        var client = app.CreateClient();

        // Set the auth cookie that the JWT bearer handler reads from
        client.DefaultRequestHeaders.Add("Cookie", $".Snakk.Auth={token}");

        return client;
    }
}
