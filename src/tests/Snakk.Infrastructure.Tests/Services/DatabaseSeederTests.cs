using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Snakk.Application.Services;
using Snakk.DbSeeder.Services;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Services;

namespace Snakk.Infrastructure.Tests.Services;

/// <summary>
/// Regression coverage for the admin-password-reset hole closed by
/// fix/dbseeder-admin-password-reset. The seeder runs on every container boot
/// (entrypoint.sh fires it with --skip-seed); before this fix it would
/// unconditionally overwrite the GlobalAdmin password and clear lockout state.
/// </summary>
public class DatabaseSeederTests : IAsyncDisposable
{
    private readonly SnakkDbContext _context;
    private readonly BCryptPasswordHasher _passwordHasher = new();
    private readonly EmailProtector _emailProtector = new(new EphemeralDataProtectionProvider());
    private readonly IAvatarGenerationService _avatarService = Substitute.For<IAvatarGenerationService>();
    private readonly IMarkupParser _markupParser = Substitute.For<IMarkupParser>();

    public DatabaseSeederTests()
    {
        var options = new DbContextOptionsBuilder<SnakkDbContext>()
            .UseInMemoryDatabase($"DbSeederTest_{Guid.NewGuid()}")
            .Options;
        _context = new SnakkDbContext(options);
    }

    private DatabaseSeeder NewSeeder(Dictionary<string, string?> config)
    {
        var configRoot = new ConfigurationBuilder().AddInMemoryCollection(config).Build();
        var mockCache = Substitute.For<HybridCache>();
        var mockFileStorage = Substitute.For<IFileStorage>();
        return new DatabaseSeeder(_context, _passwordHasher, _avatarService, _markupParser, _emailProtector, configRoot, mockCache, mockFileStorage);
    }

    // Regression: the SECOND time the seeder runs (every container restart) with no
    // explicit Setup:AdminPassword, the admin password must NOT be reset to the
    // bootstrap default. Pre-fix, this is exactly how every deploy turned the
    // GlobalAdmin password into "admin123".
    [Test]
    public async Task EnsureDefaultAdmin_SecondRun_WithoutExplicitPassword_PreservesUserPassword()
    {
        // Arrange — first boot: setup wizard wrote Setup:AdminPassword to config.
        await NewSeeder(new() { ["Setup:AdminPassword"] = "first-boot-password-from-wizard!" })
            .EnsureDefaultAdminExistsAsync();

        // Simulate "user later changes their admin password via the app".
        var admin = await _context.Users.SingleAsync();
        admin.PasswordHash = _passwordHasher.HashPassword("user-chose-this-later!");
        await _context.SaveChangesAsync();
        var hashAfterUserChange = admin.PasswordHash;

        // Act — second boot: Snakk.Setup's ScrubSensitiveConfig has removed
        // Setup:AdminPassword from snakk-config.json, so it's no longer in config.
        await NewSeeder(new()).EnsureDefaultAdminExistsAsync();

        // Assert — the user-chosen password must still be in place.
        await _context.Entry(admin).ReloadAsync();
        await Assert.That(admin.PasswordHash).IsEqualTo(hashAfterUserChange);
    }

    // Companion regression: a locked-out admin (e.g. brute-force in progress) must
    // not have FailedLoginAttempts and LockoutEnd silently reset by a restart.
    [Test]
    public async Task EnsureDefaultAdmin_SecondRun_WithoutExplicitPassword_PreservesLockout()
    {
        await NewSeeder(new() { ["Setup:AdminPassword"] = "first-boot-password-from-wizard!" })
            .EnsureDefaultAdminExistsAsync();

        var admin = await _context.Users.SingleAsync();
        admin.FailedLoginAttempts = 5;
        admin.LockoutEnd = DateTime.UtcNow.AddMinutes(15);
        await _context.SaveChangesAsync();
        var lockoutEnd = admin.LockoutEnd;

        await NewSeeder(new()).EnsureDefaultAdminExistsAsync();

        await _context.Entry(admin).ReloadAsync();
        await Assert.That(admin.FailedLoginAttempts).IsEqualTo(5);
        await Assert.That(admin.LockoutEnd).IsEqualTo(lockoutEnd);
    }

    // Escape hatch: if the operator EXPLICITLY sets Setup:AdminPassword (e.g. running
    // the seeder out-of-band to recover from a forgotten admin password), the sync
    // path still works. This is the only legitimate way to reset the admin password
    // via the seeder.
    [Test]
    public async Task EnsureDefaultAdmin_SecondRun_WithExplicitPassword_SyncsAndClearsLockout()
    {
        await NewSeeder(new() { ["Setup:AdminPassword"] = "original-password!" })
            .EnsureDefaultAdminExistsAsync();

        var admin = await _context.Users.SingleAsync();
        var originalHash = admin.PasswordHash;
        admin.FailedLoginAttempts = 5;
        admin.LockoutEnd = DateTime.UtcNow.AddMinutes(15);
        await _context.SaveChangesAsync();

        await NewSeeder(new() { ["Setup:AdminPassword"] = "operator-recovery-password!" })
            .EnsureDefaultAdminExistsAsync();

        await _context.Entry(admin).ReloadAsync();
        await Assert.That(admin.PasswordHash).IsNotEqualTo(originalHash);
        await Assert.That(_passwordHasher.VerifyPassword("operator-recovery-password!", admin.PasswordHash!)).IsTrue();
        await Assert.That(admin.FailedLoginAttempts).IsEqualTo(0);
        await Assert.That(admin.LockoutEnd).IsNull();
    }

    // First-creation path with no config — the bootstrap default applies once.
    [Test]
    public async Task EnsureDefaultAdmin_FirstRun_WithoutConfig_UsesBootstrapDefault()
    {
        await NewSeeder(new()).EnsureDefaultAdminExistsAsync();

        var admin = await _context.Users.SingleAsync();
        await Assert.That(_passwordHasher.VerifyPassword("admin123", admin.PasswordHash!)).IsTrue();
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }
}
