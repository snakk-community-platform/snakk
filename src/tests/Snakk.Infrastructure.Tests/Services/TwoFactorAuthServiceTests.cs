using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.Services;
using Snakk.Domain.ValueObjects;

namespace Snakk.Infrastructure.Tests.Services;

public class TwoFactorAuthServiceTests : IDisposable
{
    private readonly SnakkDbContext _context;
    private readonly ITotpService _totpService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITrustedDeviceService _trustedDeviceService;
    private readonly ITwoFactorSecretProtector _secretProtector;
    private readonly IDistributedCache _cache;
    private readonly ILogger<TwoFactorAuthService> _logger;
    private readonly TwoFactorAuthService _service;

    public TwoFactorAuthServiceTests()
    {
        var options = new DbContextOptionsBuilder<SnakkDbContext>()
            .UseInMemoryDatabase(databaseName: $"TwoFactorAuthTests_{Guid.NewGuid()}")
            .Options;
        _context = new SnakkDbContext(options);
        _totpService = Substitute.For<ITotpService>();
        _passwordHasher = Substitute.For<IPasswordHasher>();
        _trustedDeviceService = Substitute.For<ITrustedDeviceService>();
        _secretProtector = Substitute.For<ITwoFactorSecretProtector>();
        _logger = Substitute.For<ILogger<TwoFactorAuthService>>();

        // No replay by default: cache miss on read, no-op on write.
        _cache = Substitute.For<IDistributedCache>();
        _cache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((byte[]?)null);
        _cache.SetAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<DistributedCacheEntryOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Secret protector pass-through for tests (encrypt/decrypt are identity functions)
        _secretProtector.Protect(Arg.Any<string>()).Returns(callInfo => $"ENC:{callInfo.ArgAt<string>(0)}");
        _secretProtector.Unprotect(Arg.Any<string>()).Returns(callInfo => { var s = callInfo.ArgAt<string>(0); return s.StartsWith("ENC:") ? s[4..] : s; });

        _service = new TwoFactorAuthService(
            _context,
            _totpService,
            _passwordHasher,
            _trustedDeviceService,
            _secretProtector,
            _cache,
            _logger);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private async Task<UserDatabaseEntity> CreateTestUser(
        string publicId = "testuser",
        bool twoFactorEnabled = false,
        string? twoFactorSecret = null,
        string? passwordHash = "hashedpw")
    {
        var user = new UserDatabaseEntity
        {
            PublicId = publicId,
            DisplayName = "Test User",
            Email = "test@example.com",
            PasswordHash = passwordHash,
            TwoFactorEnabled = twoFactorEnabled,
            TwoFactorSecret = twoFactorSecret,
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return user;
    }

    #region SetupTwoFactorAsync Tests

    [Test]
    public async Task SetupTwoFactorAsync_ReturnsSecretAndQrCode()
    {
        await CreateTestUser("setup_user");
        _totpService.GenerateSecret().Returns("TESTSECRET");
        _totpService.GenerateQrCodeUri("TESTSECRET", Arg.Any<string>(), "Snakk")
            .Returns("otpauth://totp/Snakk:test@example.com?secret=TESTSECRET");

        var result = await _service.SetupTwoFactorAsync("setup_user");

        await Assert.That(result).IsNotNull();
        await Assert.That(result.Secret).IsEqualTo("TESTSECRET");
        await Assert.That(result.QrCodeUrl).Contains("otpauth://");
    }

    [Test]
    public async Task SetupTwoFactorAsync_StoresSecretOnUser()
    {
        await CreateTestUser("setup_store");
        _totpService.GenerateSecret().Returns("STOREDSECRET");
        _totpService.GenerateQrCodeUri(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns("otpauth://test");

        await _service.SetupTwoFactorAsync("setup_store");

        var user = await _context.Users.FirstAsync(u => u.PublicId == "setup_store");
        await Assert.That(user.TwoFactorSecret).StartsWith("ENC:");
    }

    [Test]
    public async Task SetupTwoFactorAsync_NonexistentUser_ThrowsException()
    {
        var act = async () => await _service.SetupTwoFactorAsync("nonexistent");
        await Assert.That(act).ThrowsException();
    }

    [Test]
    public async Task SetupTwoFactorAsync_AlreadyEnabled_ThrowsException()
    {
        await CreateTestUser("setup_already", twoFactorEnabled: true);

        var act = async () => await _service.SetupTwoFactorAsync("setup_already");
        await Assert.That(act).ThrowsException();
    }

    #endregion

    #region EnableTwoFactorAsync Tests

    [Test]
    public async Task EnableTwoFactorAsync_WithValidCode_EnablesAndReturnsBackupCodes()
    {
        await CreateTestUser("enable_user", twoFactorSecret: "SECRET123");
        _totpService.VerifyCode("SECRET123", "123456", Arg.Any<int>()).Returns(true);
        _totpService.GenerateBackupCodes(10).Returns(["CODE1", "CODE2", "CODE3"]);
        _totpService.HashBackupCode(Arg.Any<string>()).Returns("hashed");

        var (success, backupCodes, error) = await _service.EnableTwoFactorAsync("enable_user", "123456");

        await Assert.That(success).IsTrue();
        await Assert.That(backupCodes.Count).IsEqualTo(3);
        await Assert.That(error).IsNull();

        var user = await _context.Users.FirstAsync(u => u.PublicId == "enable_user");
        await Assert.That(user.TwoFactorEnabled).IsTrue();
        await Assert.That(user.TwoFactorEnabledAt).IsNotNull();
    }

    [Test]
    public async Task EnableTwoFactorAsync_WithInvalidCode_ReturnsFalse()
    {
        await CreateTestUser("enable_invalid", twoFactorSecret: "SECRET123");
        _totpService.VerifyCode("SECRET123", "000000", Arg.Any<int>()).Returns(false);

        var (success, backupCodes, error) = await _service.EnableTwoFactorAsync("enable_invalid", "000000");

        await Assert.That(success).IsFalse();
        await Assert.That(error).IsEqualTo("Invalid verification code");
    }

    [Test]
    public async Task EnableTwoFactorAsync_AlreadyEnabled_ReturnsFalse()
    {
        await CreateTestUser("enable_already", twoFactorEnabled: true);

        var (success, _, error) = await _service.EnableTwoFactorAsync("enable_already", "123456");

        await Assert.That(success).IsFalse();
        await Assert.That(error).IsEqualTo("2FA is already enabled");
    }

    [Test]
    public async Task EnableTwoFactorAsync_NoSetupInitiated_ReturnsFalse()
    {
        await CreateTestUser("enable_nosetup");

        var (success, _, error) = await _service.EnableTwoFactorAsync("enable_nosetup", "123456");

        await Assert.That(success).IsFalse();
        await Assert.That(error).Contains("setup not initiated");
    }

    [Test]
    public async Task EnableTwoFactorAsync_NonexistentUser_ReturnsFalse()
    {
        var (success, _, error) = await _service.EnableTwoFactorAsync("nonexistent", "123456");

        await Assert.That(success).IsFalse();
        await Assert.That(error).IsEqualTo("User not found");
    }

    [Test]
    public async Task EnableTwoFactorAsync_StoresBackupCodesInDatabase()
    {
        await CreateTestUser("enable_backup", twoFactorSecret: "SECRET");
        _totpService.VerifyCode(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>()).Returns(true);
        _totpService.GenerateBackupCodes(10).Returns(
            Enumerable.Range(1, 10)
                .Select(i => $"CODE{i}")
                .ToList());
        _totpService.HashBackupCode(Arg.Any<string>()).Returns("hashed");

        await _service.EnableTwoFactorAsync("enable_backup", "123456");

        var codes = await _context.TwoFactorBackupCodes
            .Where(bc => bc.User.PublicId == "enable_backup")
            .ToListAsync();
        await Assert.That(codes.Count).IsEqualTo(10);
    }

    #endregion

    #region DisableTwoFactorAsync Tests

    [Test]
    public async Task DisableTwoFactorAsync_WithValidPassword_DisablesAndCleansUp()
    {
        var user = await CreateTestUser("disable_user", twoFactorEnabled: true, twoFactorSecret: "SECRET", passwordHash: "hashedpw");

        // Add backup codes
        _context.TwoFactorBackupCodes.Add(new TwoFactorBackupCodeDatabaseEntity
        {
            PublicId = Ulid.NewUlid().ToString(),
            UserId = user.Id,
            CodeHash = "hash1",
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        _passwordHasher.VerifyPassword("password", "hashedpw").Returns(true);

        var result = await _service.DisableTwoFactorAsync("disable_user", "password");

        await Assert.That(result).IsTrue();

        var updatedUser = await _context.Users.FirstAsync(u => u.PublicId == "disable_user");
        await Assert.That(updatedUser.TwoFactorEnabled).IsFalse();
        await Assert.That(updatedUser.TwoFactorSecret).IsNull();
        await Assert.That(updatedUser.TwoFactorEnabledAt).IsNull();
    }

    [Test]
    public async Task DisableTwoFactorAsync_WithWrongPassword_ReturnsFalse()
    {
        await CreateTestUser("disable_wrongpw", twoFactorEnabled: true, passwordHash: "hashedpw");
        _passwordHasher.VerifyPassword("wrong", "hashedpw").Returns(false);

        var result = await _service.DisableTwoFactorAsync("disable_wrongpw", "wrong");

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task DisableTwoFactorAsync_NotEnabled_ReturnsFalse()
    {
        await CreateTestUser("disable_notenabled");

        var result = await _service.DisableTwoFactorAsync("disable_notenabled", "password");

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task DisableTwoFactorAsync_NonexistentUser_ReturnsFalse()
    {
        var result = await _service.DisableTwoFactorAsync("nonexistent", "password");

        await Assert.That(result).IsFalse();
    }

    #endregion

    #region GetTwoFactorStatusAsync Tests

    [Test]
    public async Task GetTwoFactorStatusAsync_ReturnsStatus()
    {
        var user = await CreateTestUser("status_user", twoFactorEnabled: true);
        _context.TwoFactorBackupCodes.Add(new TwoFactorBackupCodeDatabaseEntity
        {
            PublicId = Ulid.NewUlid().ToString(),
            UserId = user.Id,
            CodeHash = "hash1",
            CreatedAt = DateTime.UtcNow
        });
        _context.TwoFactorBackupCodes.Add(new TwoFactorBackupCodeDatabaseEntity
        {
            PublicId = Ulid.NewUlid().ToString(),
            UserId = user.Id,
            CodeHash = "hash2",
            IsUsed = true,
            UsedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var status = await _service.GetTwoFactorStatusAsync("status_user");

        await Assert.That(status).IsNotNull();
        await Assert.That(status!.IsEnabled).IsTrue();
        await Assert.That(status.HasBackupCodes).IsTrue();
        await Assert.That(status.UsedBackupCodesCount).IsEqualTo(1);
        await Assert.That(status.TotalBackupCodes).IsEqualTo(2);
    }

    [Test]
    public async Task GetTwoFactorStatusAsync_NonexistentUser_ReturnsNull()
    {
        var status = await _service.GetTwoFactorStatusAsync("nonexistent");

        await Assert.That(status).IsNull();
    }

    #endregion

    #region VerifyTwoFactorCodeAsync Tests

    [Test]
    public async Task VerifyTwoFactorCodeAsync_WithValidTotpCode_ReturnsValid()
    {
        await CreateTestUser("verify_totp", twoFactorEnabled: true, twoFactorSecret: "SECRET");
        _totpService.TryVerifyCode("SECRET", "123456", out Arg.Any<long>(), Arg.Any<int>())
            .Returns(ci => { ci[2] = 12345L; return true; });

        var (isValid, usedBackup) = await _service.VerifyTwoFactorCodeAsync("verify_totp", "123456");

        await Assert.That(isValid).IsTrue();
        await Assert.That(usedBackup).IsFalse();
    }

    [Test]
    public async Task VerifyTwoFactorCodeAsync_WithValidBackupCode_ReturnsValidAndMarksUsed()
    {
        var user = await CreateTestUser("verify_backup", twoFactorEnabled: true, twoFactorSecret: "SECRET");
        _totpService.VerifyCode(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>()).Returns(false);
        _totpService.VerifyBackupCode("BACKUPCODE", "backhash").Returns(true);

        _context.TwoFactorBackupCodes.Add(new TwoFactorBackupCodeDatabaseEntity
        {
            PublicId = Ulid.NewUlid().ToString(),
            UserId = user.Id,
            CodeHash = "backhash",
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var (isValid, usedBackup) = await _service.VerifyTwoFactorCodeAsync("verify_backup", "BACKUPCODE", "1.2.3.4");

        await Assert.That(isValid).IsTrue();
        await Assert.That(usedBackup).IsTrue();

        var code = await _context.TwoFactorBackupCodes.FirstAsync(bc => bc.User.PublicId == "verify_backup");
        await Assert.That(code.IsUsed).IsTrue();
        await Assert.That(code.UsedIp).IsEqualTo("1.2.3.4");
    }

    [Test]
    public async Task VerifyTwoFactorCodeAsync_WithInvalidCode_ReturnsFalse()
    {
        await CreateTestUser("verify_invalid", twoFactorEnabled: true, twoFactorSecret: "SECRET");
        _totpService.VerifyCode(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>()).Returns(false);

        var (isValid, _) = await _service.VerifyTwoFactorCodeAsync("verify_invalid", "000000");

        await Assert.That(isValid).IsFalse();
    }

    [Test]
    public async Task VerifyTwoFactorCodeAsync_TwoFactorNotEnabled_ReturnsFalse()
    {
        await CreateTestUser("verify_disabled");

        var (isValid, _) = await _service.VerifyTwoFactorCodeAsync("verify_disabled", "123456");

        await Assert.That(isValid).IsFalse();
    }

    [Test]
    public async Task VerifyTwoFactorCodeAsync_NonexistentUser_ReturnsFalse()
    {
        var (isValid, _) = await _service.VerifyTwoFactorCodeAsync("nonexistent", "123456");

        await Assert.That(isValid).IsFalse();
    }

    #endregion

    #region RegenerateBackupCodesAsync Tests

    [Test]
    public async Task RegenerateBackupCodesAsync_ReplacesOldCodes()
    {
        var user = await CreateTestUser("regen_user", twoFactorEnabled: true, passwordHash: "hashedpw");
        _context.TwoFactorBackupCodes.Add(new TwoFactorBackupCodeDatabaseEntity
        {
            PublicId = Ulid.NewUlid().ToString(),
            UserId = user.Id,
            CodeHash = "oldhash",
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        _passwordHasher.VerifyPassword("password", "hashedpw").Returns(true);
        _totpService.GenerateBackupCodes(10).Returns(["NEW1", "NEW2"]);
        _totpService.HashBackupCode(Arg.Any<string>()).Returns("newhash");

        var codes = await _service.RegenerateBackupCodesAsync("regen_user", "password");

        await Assert.That(codes.Count).IsEqualTo(2);

        var dbCodes = await _context.TwoFactorBackupCodes
            .Where(bc => bc.UserId == user.Id)
            .ToListAsync();
        await Assert.That(dbCodes.Count).IsEqualTo(2);
    }

    [Test]
    public async Task RegenerateBackupCodesAsync_WithWrongPassword_Throws()
    {
        await CreateTestUser("regen_wrongpw", twoFactorEnabled: true, passwordHash: "hashedpw");
        _passwordHasher.VerifyPassword("wrong", "hashedpw").Returns(false);

        var act = async () => await _service.RegenerateBackupCodesAsync("regen_wrongpw", "wrong");

        await Assert.That(act).ThrowsException();
    }

    [Test]
    public async Task RegenerateBackupCodesAsync_TwoFactorNotEnabled_Throws()
    {
        await CreateTestUser("regen_disabled");

        var act = async () => await _service.RegenerateBackupCodesAsync("regen_disabled", "password");

        await Assert.That(act).ThrowsException();
    }

    #endregion

    #region Device Trust Delegation Tests

    [Test]
    public async Task TrustDeviceAsync_DelegatesToTrustedDeviceService()
    {
        await _service.TrustDeviceAsync("user1", "fp123", "Chrome", "1.2.3.4", 30);

        _trustedDeviceService.Received(1).TrustDeviceAsync(Arg.Any<UserId>(), "fp123", "Chrome", "1.2.3.4", 30);
    }

    [Test]
    public async Task IsDeviceTrustedAsync_DelegatesToTrustedDeviceService()
    {
        _trustedDeviceService.IsDeviceTrustedAsync(Arg.Any<UserId>(), "fp123")
            .Returns(true);

        var result = await _service.IsDeviceTrustedAsync("user1", "fp123");

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task RevokeDeviceAsync_DelegatesToTrustedDeviceService()
    {
        await _service.RevokeDeviceAsync("device123", "User request");

        _trustedDeviceService.Received(1).RevokeDeviceAsync("device123", "User request");
    }

    #endregion
}
