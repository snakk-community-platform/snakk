using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Snakk.Api.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Snakk.Api.Tests.Services;

public class JwtTokenServiceTests
{
    private const string TestSecret = "ThisIsATestSecretKeyThatIsAtLeast32CharactersLong!";
    private const string TestIssuer = "TestIssuer";
    private const string TestAudience = "TestAudience";

    private static JwtTokenService CreateService(
        string? secretKey = TestSecret,
        string? issuer = TestIssuer,
        string? audience = TestAudience,
        string? expirationMinutes = "60")
    {
        var configEntries = new Dictionary<string, string?>();

        if (secretKey is not null)
            configEntries["Jwt:SecretKey"] = secretKey;
        if (issuer is not null)
            configEntries["Jwt:Issuer"] = issuer;
        if (audience is not null)
            configEntries["Jwt:Audience"] = audience;
        if (expirationMinutes is not null)
            configEntries["Jwt:ExpirationMinutes"] = expirationMinutes;

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configEntries)
            .Build();

        return new JwtTokenService(config, new MemoryCache(new MemoryCacheOptions()));
    }

    [Test]
    public async Task GenerateToken_ReturnsNonEmptyString()
    {
        var service = CreateService();
        var token = service.GenerateToken("user1", "User One", "user@test.com", true, null);
        await Assert.That(token).IsNotNull();
        await Assert.That(token.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task GenerateToken_ContainsUserIdClaim()
    {
        var service = CreateService();
        var token = service.GenerateToken("user-abc-123", "User One", "user@test.com", true, null);
        var principal = service.ValidateToken(token);

        await Assert.That(principal).IsNotNull();
        var userId = principal!.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        await Assert.That(userId).IsEqualTo("user-abc-123");
    }

    [Test]
    public async Task GenerateToken_ContainsDisplayNameClaim()
    {
        var service = CreateService();
        var token = service.GenerateToken("user1", "TestDisplayName", "user@test.com", true, null);
        var principal = service.ValidateToken(token);

        await Assert.That(principal).IsNotNull();
        var displayName = principal!.FindFirst(ClaimTypes.Name)?.Value;
        await Assert.That(displayName).IsEqualTo("TestDisplayName");
    }

    [Test]
    public async Task GenerateToken_WithEmail_ContainsEmailClaim()
    {
        var service = CreateService();
        var token = service.GenerateToken("user1", "User One", "specific@email.com", true, null);
        var principal = service.ValidateToken(token);

        await Assert.That(principal).IsNotNull();
        var email = principal!.FindFirst(ClaimTypes.Email)?.Value;
        await Assert.That(email).IsEqualTo("specific@email.com");
    }

    [Test]
    public async Task GenerateToken_WithoutEmail_NoEmailClaim()
    {
        var service = CreateService();
        var token = service.GenerateToken("user1", "User One", null, false, null);
        var principal = service.ValidateToken(token);

        await Assert.That(principal).IsNotNull();
        var email = principal!.FindFirst(ClaimTypes.Email)?.Value;
        await Assert.That(email).IsNull();
    }

    [Test]
    public async Task GenerateToken_WithRole_ContainsRoleClaim()
    {
        var service = CreateService();
        var token = service.GenerateToken("user1", "User One", "user@test.com", true, null, "Admin");
        var principal = service.ValidateToken(token);

        await Assert.That(principal).IsNotNull();
        var role = principal!.FindFirst(ClaimTypes.Role)?.Value;
        await Assert.That(role).IsEqualTo("Admin");
    }

    [Test]
    public async Task GenerateToken_WithOAuthProvider_ContainsProviderClaim()
    {
        var service = CreateService();
        var token = service.GenerateToken("user1", "User One", "user@test.com", true, "Google");
        var principal = service.ValidateToken(token);

        await Assert.That(principal).IsNotNull();
        var provider = principal!.FindFirst("OAuthProvider")?.Value;
        await Assert.That(provider).IsEqualTo("Google");
    }

    [Test]
    public async Task ValidateToken_ValidToken_ReturnsPrincipal()
    {
        var service = CreateService();
        var token = service.GenerateToken("user1", "User One", "user@test.com", true, null, "Admin");
        var principal = service.ValidateToken(token);

        await Assert.That(principal).IsNotNull();
        await Assert.That(principal!.Identity!.IsAuthenticated).IsTrue();
    }

    [Test]
    public async Task ValidateToken_ExpiredToken_ReturnsNull()
    {
        // Create a service with negative expiration to generate an already-expired token
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = TestSecret,
                ["Jwt:Issuer"] = TestIssuer,
                ["Jwt:Audience"] = TestAudience,
                ["Jwt:ExpirationMinutes"] = "-1"
            })
            .Build();

        var service = new JwtTokenService(config, new MemoryCache(new MemoryCacheOptions()));
        var token = service.GenerateToken("user1", "User One", "user@test.com", true, null);

        var principal = service.ValidateToken(token);
        await Assert.That(principal).IsNull();
    }

    [Test]
    public async Task ValidateToken_InvalidSignature_ReturnsNull()
    {
        // Generate token with one key, validate with another
        var service1 = CreateService(secretKey: "FirstSecretKeyThatIsAtLeast32CharactersLong!!!!");
        var service2 = CreateService(secretKey: "SecondSecretKeyThatIsAtLeast32CharactersLong!!!");

        var token = service1.GenerateToken("user1", "User One", "user@test.com", true, null);
        var principal = service2.ValidateToken(token);

        await Assert.That(principal).IsNull();
    }

    [Test]
    public async Task ValidateToken_EmptyToken_ReturnsNull()
    {
        var service = CreateService();
        var principal = service.ValidateToken("");
        await Assert.That(principal).IsNull();
    }

    [Test]
    public async Task Constructor_MissingSecretKey_ThrowsInvalidOperationException()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = TestIssuer,
                ["Jwt:Audience"] = TestAudience
            })
            .Build();

        await Assert.That(() => new JwtTokenService(config, new MemoryCache(new MemoryCacheOptions())))
            .Throws<InvalidOperationException>();
    }
}
