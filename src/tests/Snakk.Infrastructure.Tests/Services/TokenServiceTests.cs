using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;
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
    private readonly IJwtTokenService _jwtTokenService;

    public TokenServiceTests()
    {
        var options = new DbContextOptionsBuilder<SnakkDbContext>()
            .UseInMemoryDatabase(databaseName: $"TokenServiceTests_{Guid.NewGuid()}")
            .Options;
        _context = new SnakkDbContext(options);

        _jwtTokenService = Substitute.For<IJwtTokenService>();
        _jwtTokenService.GenerateToken(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string?>(),
                Arg.Any<bool>(),
                Arg.Any<string?>(),
                Arg.Any<string?>())
            .Returns("mock-jwt-token");

        _tokenService = new TokenService(_context, _jwtTokenService);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

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
            TokenValue = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("token_to_revoke"))),
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            CreatedAt = DateTime.UtcNow
        };
        _context.RefreshTokens.Add(tokenEntity);
        await _context.SaveChangesAsync();

        // Act
        await _tokenService.RevokeRefreshTokenAsync("token_to_revoke", "User logout");

        // Assert
        var revokedToken = await _context.RefreshTokens.FirstAsync(t => t.PublicId == tokenEntity.PublicId);
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
        // Create a real JWT to test expiration parsing
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes("ThisIsAVerySecureSecretKeyForTestingPurposesAtLeast256Bits!!"));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(
            issuer: "Test",
            audience: "Test",
            claims: [new Claim(ClaimTypes.NameIdentifier, "user_exp")],
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: credentials);
        var token = new JwtSecurityTokenHandler().WriteToken(jwt);

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
