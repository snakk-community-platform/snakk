using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.Services;
using Snakk.Domain.ValueObjects;

namespace Snakk.Infrastructure.Tests.Services;

public class TokenServiceTests : IDisposable
{
    private readonly SnakkDbContext _context;
    private readonly TokenService _tokenService;
    private readonly IConfiguration _configuration;
    private const string TestSecretKey = "ThisIsAVerySecureSecretKeyForTestingPurposesAtLeast256Bits!!";

    public TokenServiceTests()
    {
        var options = new DbContextOptionsBuilder<SnakkDbContext>()
            .UseInMemoryDatabase(databaseName: $"TokenServiceTests_{Guid.NewGuid()}")
            .Options;
        _context = new SnakkDbContext(options);

        var configValues = new Dictionary<string, string?>
        {
            ["Jwt:SecretKey"] = TestSecretKey,
            ["Jwt:Issuer"] = "TestIssuer",
            ["Jwt:Audience"] = "TestAudience"
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        _tokenService = new TokenService(_context, _configuration);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    #region Constructor Tests

    [Test]
    public async Task Constructor_WithMissingSecretKey_ThrowsInvalidOperationException()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var options = new DbContextOptionsBuilder<SnakkDbContext>()
            .UseInMemoryDatabase($"TokenServiceTests_NoKey_{Guid.NewGuid()}")
            .Options;
        var ctx = new SnakkDbContext(options);

        await Assert.That(() => new TokenService(ctx, config)).ThrowsException();

        ctx.Dispose();
    }

    #endregion

    #region GenerateAccessToken Tests

    [Test]
    public async Task GenerateAccessToken_ReturnsValidJwt()
    {
        var user = new TokenUser
        {
            PublicId = "user123",
            DisplayName = "TestUser",
            Email = "test@example.com",
            TwoFactorEnabled = false
        };

        var token = _tokenService.GenerateAccessToken(user, ["GlobalAdmin"]);

        await Assert.That(token).IsNotNull();
        await Assert.That(token.Length).IsGreaterThan(0);

        // Verify it is a valid JWT (3 parts separated by dots)
        var parts = token.Split('.');
        await Assert.That(parts.Length).IsEqualTo(3);
    }

    [Test]
    public async Task GenerateAccessToken_ContainsCorrectClaims()
    {
        var user = new TokenUser
        {
            PublicId = "user456",
            DisplayName = "John Doe",
            Email = "john@example.com",
            TwoFactorEnabled = true
        };

        var token = _tokenService.GenerateAccessToken(user, ["GlobalAdmin", "CommunityAdmin"]);
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        await Assert.That(jwt.Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value).IsEqualTo("user456");
        await Assert.That(jwt.Claims.First(c => c.Type == ClaimTypes.Name).Value).IsEqualTo("John Doe");
        await Assert.That(jwt.Claims.First(c => c.Type == ClaimTypes.Email).Value).IsEqualTo("john@example.com");
        await Assert.That(jwt.Claims.First(c => c.Type == "2fa_enabled").Value).IsEqualTo("True");

        var roleClaims = jwt.Claims
            .Where(c => c.Type == ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList();
        await Assert.That(roleClaims).Contains("GlobalAdmin");
        await Assert.That(roleClaims).Contains("CommunityAdmin");
    }

    [Test]
    public async Task GenerateAccessToken_WithNullEmail_OmitsEmailClaim()
    {
        var user = new TokenUser
        {
            PublicId = "user789",
            DisplayName = "NoEmail",
            Email = null,
            TwoFactorEnabled = false
        };

        var token = _tokenService.GenerateAccessToken(user, []);
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        var emailClaim = jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email);
        await Assert.That(emailClaim).IsNull();
    }

    [Test]
    public async Task GenerateAccessToken_SetsCorrectIssuerAndAudience()
    {
        var user = new TokenUser
        {
            PublicId = "user001",
            DisplayName = "Test",
            Email = null,
            TwoFactorEnabled = false
        };

        var token = _tokenService.GenerateAccessToken(user, []);
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        await Assert.That(jwt.Issuer).IsEqualTo("TestIssuer");
        await Assert.That(jwt.Audiences.First()).IsEqualTo("TestAudience");
    }

    [Test]
    public async Task GenerateAccessToken_SetsExpirationIn30Minutes()
    {
        var user = new TokenUser
        {
            PublicId = "user002",
            DisplayName = "Test",
            Email = null,
            TwoFactorEnabled = false
        };

        var beforeGeneration = DateTime.UtcNow;
        var token = _tokenService.GenerateAccessToken(user, []);
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        var expectedMin = beforeGeneration.AddMinutes(29);
        var expectedMax = beforeGeneration.AddMinutes(31);

        await Assert.That(jwt.ValidTo > expectedMin).IsTrue();
        await Assert.That(jwt.ValidTo < expectedMax).IsTrue();
    }

    [Test]
    public async Task GenerateAccessToken_WithEmptyRoles_NoRoleClaims()
    {
        var user = new TokenUser
        {
            PublicId = "user003",
            DisplayName = "Test",
            Email = null,
            TwoFactorEnabled = false
        };

        var token = _tokenService.GenerateAccessToken(user, []);
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        var roleClaims = jwt.Claims
            .Where(c => c.Type == ClaimTypes.Role)
            .ToList();
        await Assert.That(roleClaims.Count).IsEqualTo(0);
    }

    #endregion

    #region GenerateRefreshToken Tests

    [Test]
    public async Task GenerateRefreshToken_ReturnsNonEmptyString()
    {
        var token = _tokenService.GenerateRefreshToken();

        await Assert.That(token).IsNotNull();
        await Assert.That(token.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task GenerateRefreshToken_ReturnsDifferentTokens()
    {
        var token1 = _tokenService.GenerateRefreshToken();
        var token2 = _tokenService.GenerateRefreshToken();

        await Assert.That(token1).IsNotEqualTo(token2);
    }

    [Test]
    public async Task GenerateRefreshToken_ReturnsBase64String()
    {
        var token = _tokenService.GenerateRefreshToken();

        // Should be valid base64
        var act = () => Convert.FromBase64String(token);
        await Assert.That(act).ThrowsNothing();
    }

    #endregion

    #region CreateRefreshTokenAsync Tests

    [Test]
    public async Task CreateRefreshTokenAsync_CreatesTokenInDatabase()
    {
        // Arrange
        var user = new UserDatabaseEntity
        {
            PublicId = "user_create_rt",
            DisplayName = "Test User",
            Email = "test@example.com",
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Act
        var result = await _tokenService.CreateRefreshTokenAsync(
            UserId.From("user_create_rt"), "Chrome", "fp123", "127.0.0.1", "Mozilla/5.0");

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Value).IsNotNull();

        var savedToken = await _context.RefreshTokens.FirstOrDefaultAsync();
        await Assert.That(savedToken).IsNotNull();
        await Assert.That(savedToken!.DeviceName).IsEqualTo("Chrome");
        await Assert.That(savedToken.IpAddress).IsEqualTo("127.0.0.1");
    }

    [Test]
    public async Task CreateRefreshTokenAsync_WithNonexistentUser_ThrowsException()
    {
        var act = async () => await _tokenService.CreateRefreshTokenAsync(
            UserId.From("nonexistent"), "Chrome", "fp123", "127.0.0.1", "Mozilla/5.0");

        await Assert.That(act).ThrowsException();
    }

    [Test]
    public async Task CreateRefreshTokenAsync_SetsCorrectExpiration()
    {
        var user = new UserDatabaseEntity
        {
            PublicId = "user_expiry_rt",
            DisplayName = "Test",
            Email = "test@example.com",
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var beforeCreate = DateTime.UtcNow;
        var result = await _tokenService.CreateRefreshTokenAsync(
            UserId.From("user_expiry_rt"), "Chrome", "fp123", "127.0.0.1", "UA", expirationDays: 30);

        await Assert.That(result.ExpiresAt > beforeCreate.AddDays(29)).IsTrue();
        await Assert.That(result.ExpiresAt < beforeCreate.AddDays(31)).IsTrue();
    }

    #endregion

    #region RevokeRefreshTokenAsync Tests

    [Test]
    public async Task RevokeRefreshTokenAsync_RevokesToken()
    {
        // Arrange
        var user = new UserDatabaseEntity
        {
            PublicId = "user_revoke",
            DisplayName = "Test",
            Email = "test@example.com",
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var tokenEntity = new RefreshTokenDatabaseEntity
        {
            PublicId = Ulid.NewUlid().ToString(),
            TokenValue = "token_to_revoke",
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            CreatedAt = DateTime.UtcNow
        };
        _context.RefreshTokens.Add(tokenEntity);
        await _context.SaveChangesAsync();

        // Act
        await _tokenService.RevokeRefreshTokenAsync("token_to_revoke", "User logout");

        // Assert
        var revokedToken = await _context.RefreshTokens.FirstAsync(t => t.TokenValue == "token_to_revoke");
        await Assert.That(revokedToken.RevokedAt).IsNotNull();
        await Assert.That(revokedToken.RevocationReason).IsEqualTo("User logout");
    }

    [Test]
    public async Task RevokeRefreshTokenAsync_WithNonexistentToken_DoesNothing()
    {
        var act = async () => await _tokenService.RevokeRefreshTokenAsync("nonexistent_token", "test");
        await Assert.That(act).ThrowsNothing();
    }

    [Test]
    public async Task RevokeRefreshTokenAsync_AlreadyRevokedToken_DoesNotUpdate()
    {
        // Arrange
        var user = new UserDatabaseEntity
        {
            PublicId = "user_already_revoked",
            DisplayName = "Test",
            Email = "test@example.com",
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var revokedAt = DateTime.UtcNow.AddHours(-1);
        var tokenEntity = new RefreshTokenDatabaseEntity
        {
            PublicId = Ulid.NewUlid().ToString(),
            TokenValue = "already_revoked_token",
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            CreatedAt = DateTime.UtcNow,
            RevokedAt = revokedAt,
            RevocationReason = "Original reason"
        };
        _context.RefreshTokens.Add(tokenEntity);
        await _context.SaveChangesAsync();

        // Act
        await _tokenService.RevokeRefreshTokenAsync("already_revoked_token", "New reason");

        // Assert - original revocation should be preserved
        var token = await _context.RefreshTokens.FirstAsync(t => t.TokenValue == "already_revoked_token");
        await Assert.That(token.RevocationReason).IsEqualTo("Original reason");
    }

    #endregion

    #region RevokeAllUserTokensAsync Tests

    [Test]
    public async Task RevokeAllUserTokensAsync_RevokesAllActiveTokens()
    {
        // Arrange
        var user = new UserDatabaseEntity
        {
            PublicId = "user_revoke_all",
            DisplayName = "Test",
            Email = "test@example.com",
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        for (var i = 0; i < 3; i++)
        {
            _context.RefreshTokens.Add(new RefreshTokenDatabaseEntity
            {
                PublicId = Ulid.NewUlid().ToString(),
                TokenValue = $"token_{i}",
                UserId = user.Id,
                ExpiresAt = DateTime.UtcNow.AddDays(30),
                CreatedAt = DateTime.UtcNow
            });
        }
        await _context.SaveChangesAsync();

        // Act
        await _tokenService.RevokeAllUserTokensAsync(UserId.From("user_revoke_all"), "Security measure");

        // Assert
        var tokens = await _context.RefreshTokens
            .Where(t => t.UserId == user.Id)
            .ToListAsync();
        foreach (var token in tokens)
        {
            await Assert.That(token.RevokedAt).IsNotNull();
            await Assert.That(token.RevocationReason).IsEqualTo("Security measure");
        }
    }

    #endregion

    #region GetTokenExpiration Tests

    [Test]
    public async Task GetTokenExpiration_WithValidToken_ReturnsExpiration()
    {
        var user = new TokenUser
        {
            PublicId = "user_exp",
            DisplayName = "Test",
            Email = null,
            TwoFactorEnabled = false
        };
        var token = _tokenService.GenerateAccessToken(user, []);

        var expiration = _tokenService.GetTokenExpiration(token);

        await Assert.That(expiration).IsNotNull();
        await Assert.That(expiration!.Value > DateTime.UtcNow).IsTrue();
    }

    [Test]
    public async Task GetTokenExpiration_WithInvalidToken_ReturnsNull()
    {
        var result = _tokenService.GetTokenExpiration("not.a.valid.jwt");

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task GetTokenExpiration_WithEmptyString_ReturnsNull()
    {
        var result = _tokenService.GetTokenExpiration("");

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task GetTokenExpiration_WithGarbage_ReturnsNull()
    {
        var result = _tokenService.GetTokenExpiration("totally-random-garbage-string");

        await Assert.That(result).IsNull();
    }

    #endregion
}
