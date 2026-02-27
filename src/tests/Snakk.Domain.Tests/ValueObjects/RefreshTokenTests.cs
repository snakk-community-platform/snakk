using Snakk.Domain.ValueObjects;

namespace Snakk.Domain.Tests.ValueObjects;

public class RefreshTokenTests
{
    [Test]
    public async Task Create_GeneratesTokenWithCorrectExpiration()
    {
        // Arrange
        var userId = UserId.New();

        // Act
        var token = RefreshToken.Create(userId, expirationDays: 30);

        // Assert
        await Assert.That(token).IsNotNull();
        await Assert.That(token.Value).IsNotNull();
        await Assert.That(token.Value.Length).IsGreaterThan(0);
        await Assert.That(token.UserId).IsEqualTo(userId);
        await Assert.That(token.CreatedAt).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
        await Assert.That(token.ExpiresAt).IsEqualTo(DateTime.UtcNow.AddDays(30)).Within(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task Create_WithCustomExpirationDays_SetsCorrectExpiration()
    {
        // Act
        var token = RefreshToken.Create(UserId.New(), expirationDays: 7);

        // Assert
        await Assert.That(token.ExpiresAt).IsEqualTo(DateTime.UtcNow.AddDays(7)).Within(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task Revoke_SetsRevokedAt()
    {
        // Arrange
        var token = RefreshToken.Create(UserId.New());

        // Act
        var revokedToken = token.Revoke();

        // Assert
        await Assert.That(revokedToken.RevokedAt).IsNotNull();
    }

    [Test]
    public async Task IsRevoked_IsTrueAfterRevoke()
    {
        // Arrange
        var token = RefreshToken.Create(UserId.New());

        // Act
        var revokedToken = token.Revoke();

        // Assert
        await Assert.That(revokedToken.IsRevoked).IsTrue();
    }

    [Test]
    public async Task IsRevoked_IsFalseForNewToken()
    {
        // Act
        var token = RefreshToken.Create(UserId.New());

        // Assert
        await Assert.That(token.IsRevoked).IsFalse();
    }

    [Test]
    public async Task IsExpired_IsTrueForExpiredTokens()
    {
        // Arrange - create a token that already expired via Rehydrate
        var token = RefreshToken.Rehydrate(
            "test-token-value",
            UserId.New(),
            expiresAt: DateTime.UtcNow.AddDays(-1),
            createdAt: DateTime.UtcNow.AddDays(-31));

        // Assert
        await Assert.That(token.IsExpired).IsTrue();
    }

    [Test]
    public async Task IsExpired_IsFalseForFreshToken()
    {
        // Act
        var token = RefreshToken.Create(UserId.New());

        // Assert
        await Assert.That(token.IsExpired).IsFalse();
    }

    [Test]
    public async Task IsActive_IsTrueOnlyWhenNotRevokedAndNotExpired()
    {
        // Arrange - fresh token
        var token = RefreshToken.Create(UserId.New());

        // Assert
        await Assert.That(token.IsActive).IsTrue();
    }

    [Test]
    public async Task IsActive_IsFalseWhenRevoked()
    {
        // Arrange
        var token = RefreshToken.Create(UserId.New());
        var revokedToken = token.Revoke();

        // Assert
        await Assert.That(revokedToken.IsActive).IsFalse();
    }

    [Test]
    public async Task IsActive_IsFalseWhenExpired()
    {
        // Arrange
        var token = RefreshToken.Rehydrate(
            "test-token-value",
            UserId.New(),
            expiresAt: DateTime.UtcNow.AddDays(-1),
            createdAt: DateTime.UtcNow.AddDays(-31));

        // Assert
        await Assert.That(token.IsActive).IsFalse();
    }

    [Test]
    public async Task Rehydrate_RestoresAllProperties()
    {
        // Arrange
        var userId = UserId.New();
        var expiresAt = DateTime.UtcNow.AddDays(15);
        var createdAt = DateTime.UtcNow.AddDays(-15);
        var revokedAt = DateTime.UtcNow.AddDays(-1).ToString("O");

        // Act
        var token = RefreshToken.Rehydrate(
            "rehydrated-token-value",
            userId,
            expiresAt,
            createdAt,
            revokedAt);

        // Assert
        await Assert.That(token.Value).IsEqualTo("rehydrated-token-value");
        await Assert.That(token.UserId).IsEqualTo(userId);
        await Assert.That(token.ExpiresAt).IsEqualTo(expiresAt);
        await Assert.That(token.CreatedAt).IsEqualTo(createdAt);
        await Assert.That(token.RevokedAt).IsEqualTo(revokedAt);
    }

    [Test]
    public async Task Revoke_ReturnsNewInstance_OriginalUnchanged()
    {
        // Arrange
        var original = RefreshToken.Create(UserId.New());

        // Act
        var revoked = original.Revoke();

        // Assert - original should still be active (records are immutable via 'with')
        await Assert.That(original.RevokedAt).IsNull();
        await Assert.That(revoked.RevokedAt).IsNotNull();
    }
}
