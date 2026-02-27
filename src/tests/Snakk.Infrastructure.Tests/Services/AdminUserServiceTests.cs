using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.Services;

namespace Snakk.Infrastructure.Tests.Services;

public class AdminUserServiceTests : IDisposable
{
    private readonly SnakkDbContext _context;
    private readonly AdminUserService _service;
    private readonly IMemoryCache _cache;

    public AdminUserServiceTests()
    {
        var options = new DbContextOptionsBuilder<SnakkDbContext>()
            .UseInMemoryDatabase(databaseName: $"AdminUserServiceTests_{Guid.NewGuid()}")
            .Options;
        _context = new SnakkDbContext(options);
        _cache = new MemoryCache(new MemoryCacheOptions());
        _service = new AdminUserService(_context, _cache, new Mock<ILogger<AdminUserService>>().Object);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
        _cache.Dispose();
    }

    private async Task<UserDatabaseEntity> CreateUser(string publicId, string displayName, string email)
    {
        var user = new UserDatabaseEntity
        {
            PublicId = publicId,
            DisplayName = displayName,
            Email = email,
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    private async Task<UserBanDatabaseEntity> CreateBan(
        int userId,
        int bannedByUserId,
        string reason,
        DateTime? expiresAt = null,
        DateTime? unbannedAt = null)
    {
        var ban = new UserBanDatabaseEntity
        {
            PublicId = $"ban-{Guid.NewGuid():N}",
            UserId = userId,
            BannedByUserId = bannedByUserId,
            Reason = reason,
            BannedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt,
            UnbannedAt = unbannedAt
        };
        _context.UserBans.Add(ban);
        await _context.SaveChangesAsync();
        return ban;
    }

    #region GetUserByIdAsync Tests

    [Test]
    public async Task GetUserById_WhenExists_ReturnsUser()
    {
        await CreateUser("user-001", "Test User", "test@test.com");

        var result = await _service.GetUserByIdAsync("user-001");

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.PublicId).IsEqualTo("user-001");
        await Assert.That(result.DisplayName).IsEqualTo("Test User");
        await Assert.That(result.Email).IsEqualTo("test@test.com");
    }

    [Test]
    public async Task GetUserById_WhenNotFound_ReturnsNull()
    {
        var result = await _service.GetUserByIdAsync("nonexistent-user");

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task GetUserById_CachesResult()
    {
        await CreateUser("user-cache", "Cached User", "cached@test.com");

        // First call - loads from DB
        var result1 = await _service.GetUserByIdAsync("user-cache");

        // Second call - should come from cache
        var result2 = await _service.GetUserByIdAsync("user-cache");

        await Assert.That(result1).IsNotNull();
        await Assert.That(result2).IsNotNull();
        await Assert.That(result1!.PublicId).IsEqualTo(result2!.PublicId);
        await Assert.That(result1.DisplayName).IsEqualTo(result2.DisplayName);
        await Assert.That(result1.Email).IsEqualTo(result2.Email);
    }

    #endregion

    #region UserExistsAsync Tests

    [Test]
    public async Task UserExists_WhenExists_ReturnsTrue()
    {
        await CreateUser("user-exists", "Existing User", "exists@test.com");

        var result = await _service.UserExistsAsync("user-exists");

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task UserExists_WhenNotFound_ReturnsFalse()
    {
        var result = await _service.UserExistsAsync("nonexistent-user");

        await Assert.That(result).IsFalse();
    }

    #endregion

    #region GetActiveBansAsync Tests

    [Test]
    public async Task GetActiveBans_ReturnsPaginated()
    {
        var user = await CreateUser("user-001", "Test User", "test@test.com");
        var admin = await CreateUser("admin-001", "Admin", "admin@test.com");

        // Create 3 active permanent bans
        await CreateBan(user.Id, admin.Id, "Ban 1");
        await CreateBan(user.Id, admin.Id, "Ban 2");
        await CreateBan(user.Id, admin.Id, "Ban 3");

        var result = await _service.GetActiveBansAsync(page: 1, pageSize: 2);

        await Assert.That(result.Total).IsEqualTo(3);
        await Assert.That(result.Items.Count).IsEqualTo(2);
        await Assert.That(result.Page).IsEqualTo(1);
        await Assert.That(result.PageSize).IsEqualTo(2);

        // Verify second page
        var page2 = await _service.GetActiveBansAsync(page: 2, pageSize: 2);
        await Assert.That(page2.Items.Count).IsEqualTo(1);
    }

    [Test]
    public async Task GetActiveBans_ExcludesExpiredBans()
    {
        var user = await CreateUser("user-002", "Test User 2", "test2@test.com");
        var admin = await CreateUser("admin-002", "Admin 2", "admin2@test.com");

        // Active permanent ban
        await CreateBan(user.Id, admin.Id, "Active ban");

        // Expired ban (ExpiresAt in the past)
        await CreateBan(user.Id, admin.Id, "Expired ban", expiresAt: DateTime.UtcNow.AddDays(-1));

        var result = await _service.GetActiveBansAsync(page: 1, pageSize: 10);

        await Assert.That(result.Total).IsEqualTo(1);
        await Assert.That(result.Items[0].Reason).IsEqualTo("Active ban");
    }

    [Test]
    public async Task GetActiveBans_ExcludesUnbannedUsers()
    {
        var user = await CreateUser("user-003", "Test User 3", "test3@test.com");
        var admin = await CreateUser("admin-003", "Admin 3", "admin3@test.com");

        // Active ban
        await CreateBan(user.Id, admin.Id, "Active ban");

        // Unbanned (UnbannedAt is set)
        await CreateBan(user.Id, admin.Id, "Unbanned", unbannedAt: DateTime.UtcNow.AddHours(-1));

        var result = await _service.GetActiveBansAsync(page: 1, pageSize: 10);

        await Assert.That(result.Total).IsEqualTo(1);
        await Assert.That(result.Items[0].Reason).IsEqualTo("Active ban");
    }

    #endregion
}
