using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.Mappers;

namespace Snakk.Infrastructure.Tests.Mappers;

public class RefreshTokenMapperTests
{
    #region FromPersistence Tests

    [Test]
    public async Task FromPersistence_WithActiveToken_MapsAllProperties()
    {
        // Arrange
        var userPublicId = Guid.NewGuid().ToString();
        var expiresAt = DateTime.UtcNow.AddDays(30);
        var createdAt = DateTime.UtcNow.AddDays(-1);

        var entity = new RefreshTokenDatabaseEntity
        {
            Id = 1,
            PublicId = Guid.NewGuid().ToString(),
            TokenValue = "abc123tokenvalue",
            UserId = 42,
            User = new UserDatabaseEntity
            {
                PublicId = userPublicId,
                DisplayName = "TestUser",
                Email = "test@example.com",
                CreatedAt = DateTime.UtcNow,
                LastModifiedAt = DateTime.UtcNow
            },
            ExpiresAt = expiresAt,
            CreatedAt = createdAt,
            RevokedAt = null
        };

        // Act
        var token = entity.FromPersistence();

        // Assert
        await Assert.That(token).IsNotNull();
        await Assert.That(token.Value).IsEqualTo("abc123tokenvalue");
        await Assert.That(token.UserId.Value).IsEqualTo(userPublicId);
        await Assert.That(token.ExpiresAt).IsEqualTo(expiresAt);
        await Assert.That(token.CreatedAt).IsEqualTo(createdAt);
        await Assert.That(token.RevokedAt).IsNull();
        await Assert.That(token.IsRevoked).IsFalse();
    }

    [Test]
    public async Task FromPersistence_WithRevokedToken_MapsRevokedAtAsIsoString()
    {
        // Arrange
        var revokedAt = new DateTime(2025, 6, 15, 10, 30, 0, DateTimeKind.Utc);
        var entity = new RefreshTokenDatabaseEntity
        {
            Id = 2,
            PublicId = Guid.NewGuid().ToString(),
            TokenValue = "revokedtoken123",
            UserId = 42,
            User = new UserDatabaseEntity
            {
                PublicId = Guid.NewGuid().ToString(),
                DisplayName = "TestUser",
                Email = "test@example.com",
                CreatedAt = DateTime.UtcNow,
                LastModifiedAt = DateTime.UtcNow
            },
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            RevokedAt = revokedAt
        };

        // Act
        var token = entity.FromPersistence();

        // Assert
        await Assert.That(token.RevokedAt).IsNotNull();
        await Assert.That(token.RevokedAt).IsEqualTo(revokedAt.ToString("O"));
        await Assert.That(token.IsRevoked).IsTrue();
    }

    [Test]
    public async Task FromPersistence_WithNullUser_ThrowsInvalidOperationException()
    {
        // Arrange
        var entity = new RefreshTokenDatabaseEntity
        {
            Id = 3,
            PublicId = Guid.NewGuid().ToString(),
            TokenValue = "tokenvalue",
            UserId = 42,
            User = null!,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            CreatedAt = DateTime.UtcNow
        };

        // Act & Assert
        var action = () => entity.FromPersistence();
        await Assert.That(action).ThrowsExactly<InvalidOperationException>()
            .WithMessage("User navigation property must be loaded");
    }

    [Test]
    public async Task FromPersistence_WithExpiredToken_MapsCorrectly()
    {
        // Arrange - token that expired yesterday
        var expiresAt = DateTime.UtcNow.AddDays(-1);
        var entity = new RefreshTokenDatabaseEntity
        {
            Id = 4,
            PublicId = Guid.NewGuid().ToString(),
            TokenValue = "expiredtoken",
            UserId = 42,
            User = new UserDatabaseEntity
            {
                PublicId = Guid.NewGuid().ToString(),
                DisplayName = "TestUser",
                Email = "test@example.com",
                CreatedAt = DateTime.UtcNow,
                LastModifiedAt = DateTime.UtcNow
            },
            ExpiresAt = expiresAt,
            CreatedAt = DateTime.UtcNow.AddDays(-31),
            RevokedAt = null
        };

        // Act
        var token = entity.FromPersistence();

        // Assert
        await Assert.That(token.ExpiresAt).IsEqualTo(expiresAt);
        await Assert.That(token.IsExpired).IsTrue();
        await Assert.That(token.IsActive).IsFalse();
    }

    [Test]
    public async Task FromPersistence_PreservesExactTimestamps()
    {
        // Arrange
        var expiresAt = new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);
        var createdAt = new DateTime(2025, 12, 16, 12, 0, 0, DateTimeKind.Utc);

        var entity = new RefreshTokenDatabaseEntity
        {
            Id = 5,
            PublicId = Guid.NewGuid().ToString(),
            TokenValue = "timestamptoken",
            UserId = 1,
            User = new UserDatabaseEntity
            {
                PublicId = Guid.NewGuid().ToString(),
                DisplayName = "TestUser",
                Email = "test@example.com",
                CreatedAt = DateTime.UtcNow,
                LastModifiedAt = DateTime.UtcNow
            },
            ExpiresAt = expiresAt,
            CreatedAt = createdAt,
            RevokedAt = null
        };

        // Act
        var token = entity.FromPersistence();

        // Assert
        await Assert.That(token.ExpiresAt).IsEqualTo(expiresAt);
        await Assert.That(token.CreatedAt).IsEqualTo(createdAt);
    }

    #endregion

    // Note: RefreshTokenMapper intentionally has no ToPersistence method.
    // The repository handles persistence directly because it needs to look up User.Id from the database.
    // Therefore, no ToPersistence or round-trip tests are applicable.
}
