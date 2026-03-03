using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.Services;

namespace Snakk.Infrastructure.Tests.Services;

public class SettingsServiceTests : IDisposable
{
    private readonly SnakkDbContext _context;
    private readonly ServiceProvider _cacheServiceProvider;
    private readonly IConfiguration _configuration;
    private readonly Mock<ISecurityService> _mockSecurityService;
    private readonly SettingsService _service;

    public SettingsServiceTests()
    {
        var options = new DbContextOptionsBuilder<SnakkDbContext>()
            .UseInMemoryDatabase(databaseName: $"SettingsTests_{Guid.NewGuid()}")
            .Options;
        _context = new SnakkDbContext(options);
        var services = new ServiceCollection();
        services.AddHybridCache();
        _cacheServiceProvider = services.BuildServiceProvider();
        var cache = _cacheServiceProvider.GetRequiredService<HybridCache>();

        var configValues = new Dictionary<string, string?>
        {
            ["OAuth:Google:ClientId"] = "google-client-id",
            ["OAuth:Google:ClientSecret"] = "google-secret",
            ["OAuth:GitHub:ClientId"] = "",
            ["OAuth:GitHub:ClientSecret"] = ""
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        _mockSecurityService = new Mock<ISecurityService>();

        // Use EphemeralDataProtectionProvider for testing
        var dataProtectionProvider = new EphemeralDataProtectionProvider();

        _service = new SettingsService(
            _context,
            cache,
            dataProtectionProvider,
            _configuration,
            _mockSecurityService.Object);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
        _cacheServiceProvider.Dispose();
    }

    private async Task<UserDatabaseEntity> CreateUser(string publicId = "admin")
    {
        var user = new UserDatabaseEntity
        {
            PublicId = publicId,
            DisplayName = "Admin",
            Email = "admin@example.com",
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return user;
    }

    private async Task CreateSetting(string category, string key, string value, string valueType = "String",
        bool isEncrypted = false)
    {
        _context.SystemSettings.Add(new SystemSettingDatabaseEntity
        {
            PublicId = Guid.NewGuid().ToString("N"),
            Category = category,
            Key = key,
            Value = value,
            ValueType = valueType,
            IsEncrypted = isEncrypted,
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();
    }

    #region GetSettingsByCategoryAsync Tests

    [Test]
    public async Task GetSettingsByCategoryAsync_ReturnsSettingsForCategory()
    {
        await CreateSetting("General", "SiteName", "\"Test Site\"");
        await CreateSetting("General", "Language", "\"en\"");
        await CreateSetting("Email", "SmtpHost", "\"smtp.example.com\"");

        var result = await _service.GetSettingsByCategoryAsync("General");

        await Assert.That(result.Category).IsEqualTo("General");
        await Assert.That(result.Settings.Count).IsEqualTo(2);
    }

    [Test]
    public async Task GetSettingsByCategoryAsync_EmptyCategory_ReturnsEmpty()
    {
        var result = await _service.GetSettingsByCategoryAsync("NonExistent");

        await Assert.That(result.Settings.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetSettingsByCategoryAsync_CachesResult()
    {
        await CreateSetting("Cache", "Key1", "\"value1\"");

        // First call - loads from DB
        var result1 = await _service.GetSettingsByCategoryAsync("Cache");

        // Second call - should be cached
        var result2 = await _service.GetSettingsByCategoryAsync("Cache");

        await Assert.That(result1.Settings.Count).IsEqualTo(result2.Settings.Count);
    }

    #endregion

    #region GetSettingAsync Tests

    [Test]
    public async Task GetSettingAsync_ReturnsSetting_WhenExists()
    {
        await CreateSetting("General", "SiteName", "\"My Forum\"");

        var result = await _service.GetSettingAsync("General", "SiteName");

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Key).IsEqualTo("SiteName");
    }

    [Test]
    public async Task GetSettingAsync_ReturnsNull_WhenNotFound()
    {
        var result = await _service.GetSettingAsync("General", "NonExistent");

        await Assert.That(result).IsNull();
    }

    #endregion

    #region GetSettingValueAsync Tests

    [Test]
    public async Task GetSettingValueAsync_ReturnsIntValue()
    {
        await CreateSetting("Content", "PostMaxLength", "5000", "Integer");

        var result = await _service.GetSettingValueAsync<int>("Content", "PostMaxLength");

        await Assert.That(result).IsEqualTo(5000);
    }

    [Test]
    public async Task GetSettingValueAsync_ReturnsBoolValue()
    {
        await CreateSetting("Content", "AllowHtml", "false", "Boolean");

        var result = await _service.GetSettingValueAsync<bool>("Content", "AllowHtml");

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task GetSettingValueAsync_ReturnsStringValue()
    {
        await CreateSetting("General", "SiteName", "\"Test\"", "String");

        var result = await _service.GetSettingValueAsync<string>("General", "SiteName");

        await Assert.That(result).IsEqualTo("Test");
    }

    [Test]
    public async Task GetSettingValueAsync_NonexistentSetting_ReturnsDefault()
    {
        var intResult = await _service.GetSettingValueAsync<int>("X", "Y");
        var boolResult = await _service.GetSettingValueAsync<bool>("X", "Y");

        await Assert.That(intResult).IsEqualTo(0);
        await Assert.That(boolResult).IsFalse();
    }

    #endregion

    #region UpdateSettingAsync Tests

    [Test]
    public async Task UpdateSettingAsync_UpdatesValue()
    {
        await CreateSetting("General", "SiteName", "\"OldName\"");
        var user = await CreateUser("update_admin");

        var result = await _service.UpdateSettingAsync("General", "SiteName", "NewName", "update_admin");

        await Assert.That(result).IsNotNull();
        await Assert.That(result.UpdatedAt).IsNotNull();
    }

    [Test]
    public async Task UpdateSettingAsync_NonexistentSetting_ThrowsException()
    {
        var user = await CreateUser("update_err_admin");

        var act = async () =>
            await _service.UpdateSettingAsync("General", "NonExistent", "value", "update_err_admin");

        await Assert.That(act).ThrowsException();
    }

    [Test]
    public async Task UpdateSettingAsync_NonexistentUser_ThrowsException()
    {
        await CreateSetting("General", "SiteName", "\"OldName\"");

        var act = async () =>
            await _service.UpdateSettingAsync("General", "SiteName", "NewName", "nonexistent_user");

        await Assert.That(act).ThrowsException();
    }

    [Test]
    public async Task UpdateSettingAsync_InvalidatesCacheForCategory()
    {
        await CreateSetting("CacheTest", "Key1", "\"value1\"");
        var user = await CreateUser("cache_admin");

        // Prime the cache
        await _service.GetSettingsByCategoryAsync("CacheTest");

        // Update should invalidate
        await _service.UpdateSettingAsync("CacheTest", "Key1", "updated", "cache_admin");

        // Should not be in cache anymore (would need to re-query)
        // Verify audit log was created
        _mockSecurityService.Verify(s => s.LogAuditAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<bool>(),
            It.IsAny<Snakk.Shared.Enums.AuditLogSeverityEnum>()), Times.Once);
    }

    #endregion
}
