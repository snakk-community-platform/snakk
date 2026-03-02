using Microsoft.EntityFrameworkCore;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.Services;

namespace Snakk.Infrastructure.Tests.Services;

public class TrustedDeviceServiceTests : IDisposable
{
    private readonly SnakkDbContext _context;
    private readonly TrustedDeviceService _service;

    public TrustedDeviceServiceTests()
    {
        var options = new DbContextOptionsBuilder<SnakkDbContext>()
            .UseInMemoryDatabase(databaseName: $"TrustedDeviceServiceTests_{Guid.NewGuid()}")
            .Options;
        _context = new SnakkDbContext(options);
        _service = new TrustedDeviceService(_context);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private async Task<UserDatabaseEntity> CreateTestUserAsync(string publicId = "test-user", string displayName = "Test User", string email = "test@test.com")
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

    private async Task<TrustedDeviceDatabaseEntity> CreateTrustedDeviceAsync(
        int userId,
        string publicId = "device-001",
        string fingerprint = "fingerprint123",
        string deviceName = "Chrome on Windows",
        DateTime? revokedAt = null,
        DateTime? expiresAt = null)
    {
        var device = new TrustedDeviceDatabaseEntity
        {
            PublicId = publicId,
            UserId = userId,
            DeviceFingerprint = fingerprint,
            DeviceName = deviceName,
            TrustedAt = DateTime.UtcNow,
            LastUsedAt = DateTime.UtcNow,
            LastUsedIp = "192.168.1.1",
            RevokedAt = revokedAt,
            ExpiresAt = expiresAt
        };
        _context.TrustedDevices.Add(device);
        await _context.SaveChangesAsync();

        return device;
    }

    #region GenerateDeviceFingerprint Tests

    [Test]
    public async Task GenerateDeviceFingerprint_ReturnsSameHashForSameInput()
    {
        var result1 = _service.GenerateDeviceFingerprint("Mozilla/5.0 Chrome", "192.168.1.1");
        var result2 = _service.GenerateDeviceFingerprint("Mozilla/5.0 Chrome", "192.168.1.1");

        await Assert.That(result1).IsEqualTo(result2);
    }

    [Test]
    public async Task GenerateDeviceFingerprint_ReturnsDifferentHashForDifferentInput()
    {
        var result1 = _service.GenerateDeviceFingerprint("Mozilla/5.0 Chrome", "192.168.1.1");
        var result2 = _service.GenerateDeviceFingerprint("Mozilla/5.0 Firefox", "192.168.1.2");

        await Assert.That(result1).IsNotEqualTo(result2);
    }

    #endregion

    #region GetDeviceName Tests

    [Test]
    public async Task GetDeviceName_DetectsChrome()
    {
        var userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

        var result = _service.GetDeviceName(userAgent);

        await Assert.That(result).IsEqualTo("Chrome on Windows");
    }

    [Test]
    public async Task GetDeviceName_DetectsFirefox()
    {
        var userAgent = "Mozilla/5.0 (X11; Linux x86_64; rv:121.0) Gecko/20100101 Firefox/121.0";

        var result = _service.GetDeviceName(userAgent);

        await Assert.That(result).IsEqualTo("Firefox on Linux");
    }

    [Test]
    public async Task GetDeviceName_DetectsEdge()
    {
        // Edge UA contains both "Chrome" and "Edg" - must detect Edge, not Chrome
        var userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36 Edg/120.0.0.0";

        var result = _service.GetDeviceName(userAgent);

        await Assert.That(result).IsEqualTo("Edge on Windows");
    }

    #endregion

    #region IsDeviceTrustedAsync Tests

    [Test]
    public async Task IsDeviceTrusted_WhenTrusted_ReturnsTrue()
    {
        var user = await CreateTestUserAsync();
        var fingerprint = "trusted-fingerprint";
        await CreateTrustedDeviceAsync(user.Id, fingerprint: fingerprint);

        var result = await _service.IsDeviceTrustedAsync(UserId.From(user.PublicId), fingerprint);

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsDeviceTrusted_WhenRevoked_ReturnsFalse()
    {
        var user = await CreateTestUserAsync();
        var fingerprint = "revoked-fingerprint";
        await CreateTrustedDeviceAsync(user.Id, fingerprint: fingerprint, revokedAt: DateTime.UtcNow);

        var result = await _service.IsDeviceTrustedAsync(UserId.From(user.PublicId), fingerprint);

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task IsDeviceTrusted_WhenUserNotFound_ReturnsFalse()
    {
        var result = await _service.IsDeviceTrustedAsync(UserId.From("nonexistent-user"), "some-fingerprint");

        await Assert.That(result).IsFalse();
    }

    #endregion

    #region TrustDeviceAsync Tests

    [Test]
    public async Task TrustDevice_CreatesNewDevice()
    {
        var user = await CreateTestUserAsync();
        var userId = UserId.From(user.PublicId);

        await _service.TrustDeviceAsync(userId, "new-fingerprint", "Chrome on Windows", "10.0.0.1", expirationDays: 30);

        var device = await _context.TrustedDevices.FirstOrDefaultAsync(d => d.UserId == user.Id);
        await Assert.That(device).IsNotNull();
        await Assert.That(device!.DeviceFingerprint).IsEqualTo("new-fingerprint");
        await Assert.That(device.DeviceName).IsEqualTo("Chrome on Windows");
        await Assert.That(device.LastUsedIp).IsEqualTo("10.0.0.1");
        await Assert.That(device.ExpiresAt).IsNotNull();
        await Assert.That(device.RevokedAt).IsNull();
        await Assert.That(device.PublicId).IsNotNull();
    }

    [Test]
    public async Task TrustDevice_UpdatesExistingDevice()
    {
        var user = await CreateTestUserAsync();
        var fingerprint = "existing-fingerprint";
        var originalDevice = await CreateTrustedDeviceAsync(user.Id, fingerprint: fingerprint);
        var originalLastUsedAt = originalDevice.LastUsedAt;

        // Small delay so LastUsedAt will differ
        await Task.Delay(50);

        await _service.TrustDeviceAsync(UserId.From(user.PublicId), fingerprint, "Chrome on Windows", "10.0.0.2", expirationDays: 60);

        var devices = await _context.TrustedDevices
            .Where(d => d.UserId == user.Id && d.DeviceFingerprint == fingerprint)
            .ToListAsync();
        await Assert.That(devices.Count).IsEqualTo(1);
        await Assert.That(devices[0].LastUsedIp).IsEqualTo("10.0.0.2");
        await Assert.That(devices[0].LastUsedAt).IsNotEqualTo(originalLastUsedAt);
    }

    [Test]
    public async Task TrustDevice_WhenUserNotFound_ThrowsInvalidOperationException()
    {
        var act = async () => await _service.TrustDeviceAsync(
            UserId.From("nonexistent-user"), "fp", "device", "1.2.3.4");

        await Assert.That(act).ThrowsException();
    }

    #endregion

    #region GetTrustedDevicesAsync Tests

    [Test]
    public async Task GetTrustedDevices_ReturnsNonRevokedDevices()
    {
        var user = await CreateTestUserAsync();

        // Active device
        await CreateTrustedDeviceAsync(user.Id, publicId: "active-device", fingerprint: "fp-active");
        // Revoked device
        await CreateTrustedDeviceAsync(user.Id, publicId: "revoked-device", fingerprint: "fp-revoked", revokedAt: DateTime.UtcNow);

        var result = await _service.GetTrustedDevicesAsync(UserId.From(user.PublicId));

        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].PublicId).IsEqualTo("active-device");
        await Assert.That(result[0].IsActive).IsTrue();
    }

    [Test]
    public async Task GetTrustedDevices_WhenUserNotFound_ReturnsEmptyList()
    {
        var result = await _service.GetTrustedDevicesAsync(UserId.From("nonexistent-user"));

        await Assert.That(result.Count).IsEqualTo(0);
    }

    #endregion

    #region RevokeDeviceAsync Tests

    [Test]
    public async Task RevokeDevice_SetsRevokedAtAndReason()
    {
        var user = await CreateTestUserAsync();
        var device = await CreateTrustedDeviceAsync(user.Id, publicId: "device-to-revoke");

        await _service.RevokeDeviceAsync("device-to-revoke", "User requested revocation");

        var revokedDevice = await _context.TrustedDevices.FirstAsync(d => d.PublicId == "device-to-revoke");
        await Assert.That(revokedDevice.RevokedAt).IsNotNull();
        await Assert.That(revokedDevice.RevocationReason).IsEqualTo("User requested revocation");
    }

    #endregion

    #region RevokeAllDevicesAsync Tests

    [Test]
    public async Task RevokeAllDevices_RevokesAllForUser()
    {
        var user = await CreateTestUserAsync();
        await CreateTrustedDeviceAsync(user.Id, publicId: "dev-1", fingerprint: "fp-1");
        await CreateTrustedDeviceAsync(user.Id, publicId: "dev-2", fingerprint: "fp-2");
        await CreateTrustedDeviceAsync(user.Id, publicId: "dev-3", fingerprint: "fp-3");

        await _service.RevokeAllDevicesAsync(UserId.From(user.PublicId), "Password changed");

        var devices = await _context.TrustedDevices
            .Where(d => d.UserId == user.Id)
            .ToListAsync();
        await Assert.That(devices.Count).IsEqualTo(3);

        foreach (var device in devices)
        {
            await Assert.That(device.RevokedAt).IsNotNull();
            await Assert.That(device.RevocationReason).IsEqualTo("Password changed");
        }
    }

    #endregion
}
