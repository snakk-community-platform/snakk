using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.Services;

namespace Snakk.Infrastructure.Tests.Services;

public class SessionManagementServiceTests : IDisposable
{
    private readonly SnakkDbContext _context;
    private readonly ITokenService _tokenService;
    private readonly SessionManagementService _service;

    public SessionManagementServiceTests()
    {
        var options = new DbContextOptionsBuilder<SnakkDbContext>()
            .UseInMemoryDatabase(databaseName: $"SessionManagementServiceTests_{Guid.NewGuid()}")
            .Options;
        _context = new SnakkDbContext(options);
        _tokenService = Substitute.For<ITokenService>();
        _service = new SessionManagementService(
            _context,
            _tokenService,
            Substitute.For<ILogger<SessionManagementService>>());
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    #region GetActiveSessionsAsync Tests

    [Test]
    public async Task GetActiveSessions_WithValidUser_ReturnsActiveSessions()
    {
        // Arrange
        var user = new UserDatabaseEntity
        {
            PublicId = "user_sessions_valid",
            DisplayName = "SessionUser",
            Email = "session@example.com",
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _context.RefreshTokens.Add(new RefreshTokenDatabaseEntity
        {
            PublicId = Ulid.NewUlid().ToString(),
            TokenValue = "token_1",
            UserId = user.Id,
            IpAddress = "192.168.1.1",
            UserAgent = "Mozilla/5.0",
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            CreatedAt = DateTime.UtcNow.AddHours(-2)
        });
        _context.RefreshTokens.Add(new RefreshTokenDatabaseEntity
        {
            PublicId = Ulid.NewUlid().ToString(),
            TokenValue = "token_2",
            UserId = user.Id,
            IpAddress = "10.0.0.1",
            UserAgent = "Chrome/120",
            ExpiresAt = DateTime.UtcNow.AddDays(15),
            CreatedAt = DateTime.UtcNow.AddHours(-1)
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetActiveSessionsAsync("user_sessions_valid");

        // Assert
        await Assert.That(result.Sessions.Count).IsEqualTo(2);
        await Assert.That(result.ActiveCount).IsEqualTo(2);
    }

    [Test]
    public async Task GetActiveSessions_WithNonexistentUser_ReturnsEmptyList()
    {
        // Act
        var result = await _service.GetActiveSessionsAsync("nonexistent_user");

        // Assert
        await Assert.That(result.Sessions.Count).IsEqualTo(0);
        await Assert.That(result.ActiveCount).IsEqualTo(0);
    }

    [Test]
    public async Task GetActiveSessions_ExcludesRevokedTokens()
    {
        // Arrange
        var user = new UserDatabaseEntity
        {
            PublicId = "user_revoked_filter",
            DisplayName = "RevokedFilter",
            Email = "revoked@example.com",
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Active token
        _context.RefreshTokens.Add(new RefreshTokenDatabaseEntity
        {
            PublicId = Ulid.NewUlid().ToString(),
            TokenValue = "active_token",
            UserId = user.Id,
            IpAddress = "192.168.1.1",
            UserAgent = "Mozilla/5.0",
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            CreatedAt = DateTime.UtcNow
        });
        // Revoked token
        _context.RefreshTokens.Add(new RefreshTokenDatabaseEntity
        {
            PublicId = Ulid.NewUlid().ToString(),
            TokenValue = "revoked_token",
            UserId = user.Id,
            IpAddress = "10.0.0.1",
            UserAgent = "Chrome/120",
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            CreatedAt = DateTime.UtcNow,
            RevokedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetActiveSessionsAsync("user_revoked_filter");

        // Assert
        await Assert.That(result.Sessions.Count).IsEqualTo(1);
        await Assert.That(result.ActiveCount).IsEqualTo(1);
    }

    [Test]
    public async Task GetActiveSessions_ExcludesExpiredTokens()
    {
        // Arrange
        var user = new UserDatabaseEntity
        {
            PublicId = "user_expired_filter",
            DisplayName = "ExpiredFilter",
            Email = "expired@example.com",
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Active token
        _context.RefreshTokens.Add(new RefreshTokenDatabaseEntity
        {
            PublicId = Ulid.NewUlid().ToString(),
            TokenValue = "active_token",
            UserId = user.Id,
            IpAddress = "192.168.1.1",
            UserAgent = "Mozilla/5.0",
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            CreatedAt = DateTime.UtcNow
        });
        // Expired token
        _context.RefreshTokens.Add(new RefreshTokenDatabaseEntity
        {
            PublicId = Ulid.NewUlid().ToString(),
            TokenValue = "expired_token",
            UserId = user.Id,
            IpAddress = "10.0.0.1",
            UserAgent = "Chrome/120",
            ExpiresAt = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow.AddDays(-31)
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetActiveSessionsAsync("user_expired_filter");

        // Assert
        await Assert.That(result.Sessions.Count).IsEqualTo(1);
        await Assert.That(result.ActiveCount).IsEqualTo(1);
    }

    [Test]
    public async Task GetActiveSessions_MarksCurrent_WhenRefreshTokenMatches()
    {
        // Arrange
        var user = new UserDatabaseEntity
        {
            PublicId = "user_current_session",
            DisplayName = "CurrentSession",
            Email = "current@example.com",
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var currentTokenValue = "current_refresh_token_value";

        _context.RefreshTokens.Add(new RefreshTokenDatabaseEntity
        {
            PublicId = Ulid.NewUlid().ToString(),
            TokenValue = currentTokenValue,
            UserId = user.Id,
            IpAddress = "192.168.1.1",
            UserAgent = "Mozilla/5.0",
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            CreatedAt = DateTime.UtcNow
        });
        _context.RefreshTokens.Add(new RefreshTokenDatabaseEntity
        {
            PublicId = Ulid.NewUlid().ToString(),
            TokenValue = "other_token_value",
            UserId = user.Id,
            IpAddress = "10.0.0.1",
            UserAgent = "Chrome/120",
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetActiveSessionsAsync("user_current_session", currentTokenValue);

        // Assert
        await Assert.That(result.Sessions.Count).IsEqualTo(2);
        var currentSession = result.Sessions.Single(s => s.IsCurrent);
        await Assert.That(currentSession).IsNotNull();
        var otherSession = result.Sessions.Single(s => !s.IsCurrent);
        await Assert.That(otherSession).IsNotNull();
    }

    #endregion

    #region RevokeSessionAsync Tests

    [Test]
    public async Task RevokeSession_WithValidSession_ReturnsTrue()
    {
        // Arrange
        var user = new UserDatabaseEntity
        {
            PublicId = "user_revoke_valid",
            DisplayName = "RevokeUser",
            Email = "revoke@example.com",
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var sessionPublicId = Ulid.NewUlid().ToString();
        var tokenValue = "token_to_revoke";

        _context.RefreshTokens.Add(new RefreshTokenDatabaseEntity
        {
            PublicId = sessionPublicId,
            TokenValue = tokenValue,
            UserId = user.Id,
            IpAddress = "192.168.1.1",
            UserAgent = "Mozilla/5.0",
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        _tokenService.RevokeRefreshTokenAsync(tokenValue, "User requested revocation")
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.RevokeSessionAsync(sessionPublicId, "user_revoke_valid");

        // Assert
        await Assert.That(result).IsTrue();
        _tokenService.Received(1).RevokeRefreshTokenAsync(tokenValue, "User requested revocation");
    }

    [Test]
    public async Task RevokeSession_WithNonexistentUser_ReturnsFalse()
    {
        // Act
        var result = await _service.RevokeSessionAsync("some_session_id", "nonexistent_user");

        // Assert
        await Assert.That(result).IsFalse();
        _tokenService.DidNotReceive().RevokeRefreshTokenAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task RevokeSession_WithSessionNotOwnedByUser_ReturnsFalse()
    {
        // Arrange
        var ownerUser = new UserDatabaseEntity
        {
            PublicId = "user_owner",
            DisplayName = "Owner",
            Email = "owner@example.com",
            CreatedAt = DateTime.UtcNow
        };
        var otherUser = new UserDatabaseEntity
        {
            PublicId = "user_other",
            DisplayName = "Other",
            Email = "other@example.com",
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.AddRange(ownerUser, otherUser);
        await _context.SaveChangesAsync();

        var sessionPublicId = Ulid.NewUlid().ToString();

        _context.RefreshTokens.Add(new RefreshTokenDatabaseEntity
        {
            PublicId = sessionPublicId,
            TokenValue = "owner_token",
            UserId = ownerUser.Id,
            IpAddress = "192.168.1.1",
            UserAgent = "Mozilla/5.0",
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        // Act - other user tries to revoke owner's session
        var result = await _service.RevokeSessionAsync(sessionPublicId, "user_other");

        // Assert
        await Assert.That(result).IsFalse();
        _tokenService.DidNotReceive().RevokeRefreshTokenAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    #endregion
}
