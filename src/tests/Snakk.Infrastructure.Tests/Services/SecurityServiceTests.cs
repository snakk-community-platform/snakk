using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.Services;
using Snakk.Shared.Enums;

namespace Snakk.Infrastructure.Tests.Services;

public class SecurityServiceTests : IDisposable
{
    private readonly SnakkDbContext _context;
    private readonly SecurityService _securityService;

    public SecurityServiceTests()
    {
        var options = new DbContextOptionsBuilder<SnakkDbContext>()
            .UseInMemoryDatabase(databaseName: $"SecurityServiceTests_{Guid.NewGuid()}")
            .Options;
        _context = new SnakkDbContext(options);
        _securityService = new SecurityService(_context);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    #region LogAuditAsync Tests

    [Test]
    public async Task LogAuditAsync_CreatesAuditLogEntry()
    {
        await _securityService.LogAuditAsync(
            action: "TestAction",
            category: "TestCategory",
            details: "Test details");

        var log = await _context.AuditLogs.FirstOrDefaultAsync();
        await Assert.That(log).IsNotNull();
        await Assert.That(log!.Action).IsEqualTo("TestAction");
        await Assert.That(log.Category).IsEqualTo("TestCategory");
        await Assert.That(log.Details).IsEqualTo("Test details");
        await Assert.That(log.Success).IsTrue();
    }

    [Test]
    public async Task LogAuditAsync_WithActorUser_SetsActorUserId()
    {
        var user = new UserDatabaseEntity
        {
            PublicId = "user_audit_actor",
            DisplayName = "Admin",
            Email = "admin@example.com",
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        await _securityService.LogAuditAsync(
            action: "Login",
            category: "Auth",
            actorUserId: "user_audit_actor");

        var log = await _context.AuditLogs.FirstOrDefaultAsync();
        await Assert.That(log!.ActorUserId).IsEqualTo(user.Id);
    }

    [Test]
    public async Task LogAuditAsync_WithNonexistentUser_SetsNullActorUserId()
    {
        await _securityService.LogAuditAsync(
            action: "Login",
            category: "Auth",
            actorUserId: "nonexistent_user");

        var log = await _context.AuditLogs.FirstOrDefaultAsync();
        await Assert.That(log!.ActorUserId).IsNull();
    }

    [Test]
    public async Task LogAuditAsync_SetsSeverityCorrectly()
    {
        await _securityService.LogAuditAsync(
            action: "FailedLogin",
            category: "Security",
            severity: AuditLogSeverityEnum.Warning);

        var log = await _context.AuditLogs.FirstOrDefaultAsync();
        await Assert.That(log!.SeverityId).IsEqualTo((int)AuditLogSeverityEnum.Warning);
    }

    [Test]
    public async Task LogAuditAsync_SetsAllOptionalFields()
    {
        await _securityService.LogAuditAsync(
            action: "BanUser",
            category: "Moderation",
            targetType: "User",
            targetId: "user123",
            details: "Spam violation",
            ipAddress: "192.168.1.1",
            userAgent: "Mozilla/5.0",
            success: false,
            severity: AuditLogSeverityEnum.Error);

        var log = await _context.AuditLogs.FirstOrDefaultAsync();
        await Assert.That(log!.TargetType).IsEqualTo("User");
        await Assert.That(log.TargetId).IsEqualTo("user123");
        await Assert.That(log.IpAddress).IsEqualTo("192.168.1.1");
        await Assert.That(log.Success).IsFalse();
        await Assert.That(log.SeverityId).IsEqualTo((int)AuditLogSeverityEnum.Error);
    }

    #endregion

    #region GetAuditLogsAsync Tests

    [Test]
    public async Task GetAuditLogsAsync_ReturnsPagedResults()
    {
        // Arrange - create 60 logs
        for (int i = 0; i < 60; i++)
        {
            _context.AuditLogs.Add(new AuditLogDatabaseEntity
            {
                PublicId = Guid.NewGuid().ToString("N"),
                Action = "TestAction",
                Category = "Test",
                CreatedAt = DateTime.UtcNow.AddMinutes(-i),
                Success = true,
                SeverityId = (int)AuditLogSeverityEnum.Info
            });
        }
        await _context.SaveChangesAsync();

        // Act
        var result = await _securityService.GetAuditLogsAsync(1);

        // Assert - page size is 50
        await Assert.That(result.Logs.Count).IsEqualTo(50);
        await Assert.That(result.Total).IsEqualTo(60);
        await Assert.That(result.Page).IsEqualTo(1);
        await Assert.That(result.PageSize).IsEqualTo(50);
    }

    [Test]
    public async Task GetAuditLogsAsync_FiltersByCategory()
    {
        _context.AuditLogs.Add(new AuditLogDatabaseEntity
        {
            PublicId = Guid.NewGuid().ToString("N"),
            Action = "Login",
            Category = "Auth",
            CreatedAt = DateTime.UtcNow,
            Success = true,
            SeverityId = 1
        });
        _context.AuditLogs.Add(new AuditLogDatabaseEntity
        {
            PublicId = Guid.NewGuid().ToString("N"),
            Action = "BanUser",
            Category = "Moderation",
            CreatedAt = DateTime.UtcNow,
            Success = true,
            SeverityId = 1
        });
        await _context.SaveChangesAsync();

        var result = await _securityService.GetAuditLogsAsync(1, category: "Auth");

        await Assert.That(result.Logs.Count).IsEqualTo(1);
        await Assert.That(result.Logs.First().Category).IsEqualTo("Auth");
    }

    [Test]
    public async Task GetAuditLogsAsync_FiltersByAction()
    {
        _context.AuditLogs.Add(new AuditLogDatabaseEntity
        {
            PublicId = Guid.NewGuid().ToString("N"),
            Action = "Login",
            Category = "Auth",
            CreatedAt = DateTime.UtcNow,
            Success = true,
            SeverityId = 1
        });
        _context.AuditLogs.Add(new AuditLogDatabaseEntity
        {
            PublicId = Guid.NewGuid().ToString("N"),
            Action = "Logout",
            Category = "Auth",
            CreatedAt = DateTime.UtcNow,
            Success = true,
            SeverityId = 1
        });
        await _context.SaveChangesAsync();

        var result = await _securityService.GetAuditLogsAsync(1, action: "Login");

        await Assert.That(result.Logs.Count).IsEqualTo(1);
        await Assert.That(result.Logs.First().Action).IsEqualTo("Login");
    }

    [Test]
    public async Task GetAuditLogsAsync_OrdersByCreatedAtDescending()
    {
        for (int i = 0; i < 3; i++)
        {
            _context.AuditLogs.Add(new AuditLogDatabaseEntity
            {
                PublicId = Guid.NewGuid().ToString("N"),
                Action = $"Action{i}",
                Category = "Test",
                CreatedAt = DateTime.UtcNow.AddMinutes(-i * 10),
                Success = true,
                SeverityId = 1
            });
        }
        await _context.SaveChangesAsync();

        var result = await _securityService.GetAuditLogsAsync(1);

        // Most recent should be first
        await Assert.That(result.Logs.First().CreatedAt >= result.Logs.Last().CreatedAt).IsTrue();
    }

    #endregion

    #region GetAuditLogByIdAsync Tests

    [Test]
    public async Task GetAuditLogByIdAsync_ReturnsLog_WhenExists()
    {
        var publicId = Guid.NewGuid().ToString("N");
        _context.AuditLogs.Add(new AuditLogDatabaseEntity
        {
            PublicId = publicId,
            Action = "TestAction",
            Category = "Test",
            CreatedAt = DateTime.UtcNow,
            Success = true,
            SeverityId = 1
        });
        await _context.SaveChangesAsync();

        var result = await _securityService.GetAuditLogByIdAsync(publicId);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Id).IsEqualTo(publicId);
    }

    [Test]
    public async Task GetAuditLogByIdAsync_ReturnsNull_WhenNotFound()
    {
        var result = await _securityService.GetAuditLogByIdAsync("nonexistent");

        await Assert.That(result).IsNull();
    }

    #endregion

    #region GetActiveSessionsAsync Tests

    [Test]
    public async Task GetActiveSessionsAsync_ReturnsActiveSessions()
    {
        var user = new UserDatabaseEntity
        {
            PublicId = "user_session",
            DisplayName = "SessionUser",
            Email = "session@example.com",
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
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            CreatedAt = DateTime.UtcNow
        });
        // Expired token
        _context.RefreshTokens.Add(new RefreshTokenDatabaseEntity
        {
            PublicId = Ulid.NewUlid().ToString(),
            TokenValue = "expired_token",
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow.AddDays(-31)
        });
        // Revoked token
        _context.RefreshTokens.Add(new RefreshTokenDatabaseEntity
        {
            PublicId = Ulid.NewUlid().ToString(),
            TokenValue = "revoked_token",
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            CreatedAt = DateTime.UtcNow,
            RevokedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var sessions = await _securityService.GetActiveSessionsAsync();

        // Only the active (non-expired, non-revoked) token should be returned
        await Assert.That(sessions.Count).IsEqualTo(1);
        await Assert.That(sessions.First().Id).IsEqualTo("active_token");
    }

    #endregion

    #region GetSuspiciousActivitiesAsync Tests

    [Test]
    public async Task GetSuspiciousActivitiesAsync_ReturnsWarningLogs()
    {
        _context.AuditLogs.Add(new AuditLogDatabaseEntity
        {
            PublicId = Guid.NewGuid().ToString("N"),
            Action = "SuspiciousLogin",
            Category = "Security",
            CreatedAt = DateTime.UtcNow,
            Success = false,
            SeverityId = (int)AuditLogSeverityEnum.Warning
        });
        _context.AuditLogs.Add(new AuditLogDatabaseEntity
        {
            PublicId = Guid.NewGuid().ToString("N"),
            Action = "NormalLogin",
            Category = "Auth",
            CreatedAt = DateTime.UtcNow,
            Success = true,
            SeverityId = (int)AuditLogSeverityEnum.Info
        });
        await _context.SaveChangesAsync();

        var activities = await _securityService.GetSuspiciousActivitiesAsync(1, hours: 24);

        await Assert.That(activities.Count).IsEqualTo(1);
        await Assert.That(activities.First().Action).IsEqualTo("SuspiciousLogin");
    }

    [Test]
    public async Task GetSuspiciousActivitiesAsync_FiltersOldActivities()
    {
        _context.AuditLogs.Add(new AuditLogDatabaseEntity
        {
            PublicId = Guid.NewGuid().ToString("N"),
            Action = "OldSuspicious",
            Category = "Security",
            CreatedAt = DateTime.UtcNow.AddHours(-48),
            Success = false,
            SeverityId = (int)AuditLogSeverityEnum.Warning
        });
        await _context.SaveChangesAsync();

        var activities = await _securityService.GetSuspiciousActivitiesAsync(1, hours: 24);

        await Assert.That(activities.Count).IsEqualTo(0);
    }

    #endregion

    #region ExportUserDataAsync Tests

    [Test]
    public async Task ExportUserDataAsync_ReturnsExportDto()
    {
        var user = new UserDatabaseEntity
        {
            PublicId = "user_export",
            DisplayName = "Export User",
            Email = "export@example.com",
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var result = await _securityService.ExportUserDataAsync("user_export", "admin_user", null, null);

        await Assert.That(result).IsNotNull();
        await Assert.That(result.ExportId).IsNotNull();
        await Assert.That(result.Message).Contains("Export User");
    }

    [Test]
    public async Task ExportUserDataAsync_WithNonexistentUser_ThrowsException()
    {
        var act = async () => await _securityService.ExportUserDataAsync("nonexistent", "admin", null, null);

        await Assert.That(act).ThrowsException();
    }

    [Test]
    public async Task ExportUserDataAsync_CreatesAuditLog()
    {
        var user = new UserDatabaseEntity
        {
            PublicId = "user_export_log",
            DisplayName = "Export Log",
            Email = "exportlog@example.com",
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        await _securityService.ExportUserDataAsync("user_export_log", "admin_user", "1.2.3.4", "UA");

        var logs = await _context.AuditLogs.ToListAsync();
        await Assert.That(logs.Count).IsEqualTo(1);
        await Assert.That(logs.First().Action).IsEqualTo("ExportUserData");
        await Assert.That(logs.First().Category).IsEqualTo("Compliance");
    }

    #endregion
}
