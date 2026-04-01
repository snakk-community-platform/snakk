using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.Services;

namespace Snakk.Infrastructure.Tests.Services;

public class AuthorizationServiceTests : IDisposable
{
    private readonly SnakkDbContext _context;
    private readonly AuthorizationService _service;

    public AuthorizationServiceTests()
    {
        var options = new DbContextOptionsBuilder<SnakkDbContext>()
            .UseInMemoryDatabase(databaseName: $"AuthorizationServiceTests_{Guid.NewGuid()}")
            .Options;
        _context = new SnakkDbContext(options);
        _service = new AuthorizationService(
            _context,
            Substitute.For<ILogger<AuthorizationService>>());
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    #region UserHas2FAEnabledAsync Tests

    [Test]
    public async Task UserHas2FAEnabled_WhenEnabled_ReturnsTrue()
    {
        // Arrange
        var user = new UserDatabaseEntity
        {
            PublicId = "user_2fa_enabled",
            DisplayName = "TwoFactorUser",
            Email = "2fa@example.com",
            CreatedAt = DateTime.UtcNow,
            TwoFactorEnabled = true
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.UserHas2FAEnabledAsync("user_2fa_enabled");

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task UserHas2FAEnabled_WhenDisabled_ReturnsFalse()
    {
        // Arrange
        var user = new UserDatabaseEntity
        {
            PublicId = "user_2fa_disabled",
            DisplayName = "NoTwoFactorUser",
            Email = "no2fa@example.com",
            CreatedAt = DateTime.UtcNow,
            TwoFactorEnabled = false
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.UserHas2FAEnabledAsync("user_2fa_disabled");

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task UserHas2FAEnabled_WhenUserNotFound_ReturnsFalse()
    {
        // Act
        var result = await _service.UserHas2FAEnabledAsync("nonexistent_user");

        // Assert
        await Assert.That(result).IsFalse();
    }

    #endregion
}
