using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Snakk.Application.DTOs.Management;
using Snakk.Application.Services;
using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Adapters;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.Services;
using Snakk.Infrastructure.Tests.Helpers;

namespace Snakk.Infrastructure.Tests.Services;

/// <summary>
/// Regression coverage for the silent-no-op-<c>SaveChanges</c> class-of-bug
/// (audit findings CR-22, CR-23, CR-25, CR-26, CR-27, CR-28, HI-22a from
/// <c>docs/SECURITY-AUDIT-2026-05-23.md</c>). The pattern: read an EF entity
/// without <c>.AsTracking()</c>, mutate properties on it, call
/// <c>SaveChangesAsync</c> — under the production
/// <c>NoTrackingWithIdentityResolution</c> setting, the change detector finds
/// no tracked entries and writes nothing. The fixes add <c>.AsTracking()</c>
/// at each site.
///
/// These tests use <see cref="NoTrackingSqliteTestDatabase"/> AND
/// <c>CreateSeparateContext()</c> for the assertion read, so they reproduce
/// production semantics — the in-memory provider's identity map and the
/// default <see cref="SqliteTestDatabase"/>'s implicit TrackAll behavior
/// both mask the bug.
/// </summary>
public class SilentNoOpClassOfBugTests : IDisposable
{
    private readonly NoTrackingSqliteTestDatabase _db = new();
    private readonly ServiceProvider _cacheProvider;
    private readonly HybridCache _hybridCache;

    public SilentNoOpClassOfBugTests()
    {
        var services = new ServiceCollection();
        services.AddMemoryCache();
        services.AddHybridCache();
        _cacheProvider = services.BuildServiceProvider();
        _hybridCache = _cacheProvider.GetRequiredService<HybridCache>();
    }

    public void Dispose()
    {
        _db.Dispose();
        _cacheProvider.Dispose();
    }

    // ───────── helpers ─────────

    private async Task<UserDatabaseEntity> CreateUserAsync(string publicId = "u-test")
    {
        var user = new UserDatabaseEntity
        {
            PublicId = publicId,
            DisplayName = "Test User",
            Email = $"{publicId}@test.local",
            EmailHash = $"{publicId}@test.local",
            CreatedAt = DateTime.UtcNow
        };
        _db.Context.Users.Add(user);
        await _db.Context.SaveChangesAsync();
        return user;
    }

    private async Task<TwoFactorTrustedDeviceDatabaseEntity> SeedDeviceAsync(int userId, string publicId)
    {
        var device = new TwoFactorTrustedDeviceDatabaseEntity
        {
            PublicId = publicId,
            UserId = userId,
            DeviceFingerprint = $"fp-{publicId}",
            DeviceName = "Chrome on Linux",
            TrustedAt = DateTime.UtcNow,
            LastUsedAt = DateTime.UtcNow,
            LastUsedIp = "10.0.0.1"
        };
        _db.Context.TwoFactorTrustedDevices.Add(device);
        await _db.Context.SaveChangesAsync();
        return device;
    }

    // ───────── CR-23: TrustedDeviceService ─────────

    [Test]
    public async Task TrustedDevice_RevokeDeviceForUser_PersistsAcrossContexts()
    {
        var user = await CreateUserAsync();
        await SeedDeviceAsync(user.Id, "device-revoke-1");

        using (var serviceContext = _db.CreateSeparateContext())
        {
            var service = new TrustedDeviceService(serviceContext);
            var ok = await service.RevokeDeviceForUserAsync("device-revoke-1", user.PublicId!, "user-action");
            await Assert.That(ok).IsTrue();
        }

        using var verifyContext = _db.CreateSeparateContext();
        var stored = await verifyContext.TwoFactorTrustedDevices
            .FirstAsync(d => d.PublicId == "device-revoke-1");
        await Assert.That(stored.RevokedAt).IsNotNull();
        await Assert.That(stored.RevocationReason).IsEqualTo("user-action");
    }

    [Test]
    public async Task TrustedDevice_RevokeAllDevices_PersistsAcrossContexts()
    {
        var user = await CreateUserAsync();
        await SeedDeviceAsync(user.Id, "dev-a");
        await SeedDeviceAsync(user.Id, "dev-b");

        using (var serviceContext = _db.CreateSeparateContext())
        {
            var service = new TrustedDeviceService(serviceContext);
            await service.RevokeAllDevicesAsync(UserId.From(user.PublicId!), "password-changed");
        }

        using var verifyContext = _db.CreateSeparateContext();
        var devices = await verifyContext.TwoFactorTrustedDevices
            .Where(d => d.UserId == user.Id).ToListAsync();
        await Assert.That(devices.Count).IsEqualTo(2);
        foreach (var d in devices)
        {
            await Assert.That(d.RevokedAt).IsNotNull();
            await Assert.That(d.RevocationReason).IsEqualTo("password-changed");
        }
    }

    [Test]
    public async Task TrustedDevice_TrustDevice_UpdatesExistingRow_PersistsAcrossContexts()
    {
        var user = await CreateUserAsync();
        await SeedDeviceAsync(user.Id, "dev-trust");
        // Pre-mutation snapshot so we can verify a real change happened.
        var fingerprint = $"fp-dev-trust";

        using (var serviceContext = _db.CreateSeparateContext())
        {
            var service = new TrustedDeviceService(serviceContext);
            await service.TrustDeviceAsync(
                UserId.From(user.PublicId!), fingerprint, "Chrome on Linux",
                ipAddress: "192.168.99.99", expirationDays: 30);
        }

        using var verifyContext = _db.CreateSeparateContext();
        var stored = await verifyContext.TwoFactorTrustedDevices
            .Where(d => d.UserId == user.Id && d.DeviceFingerprint == fingerprint)
            .ToListAsync();
        await Assert.That(stored.Count).IsEqualTo(1); // updated existing, didn't add a duplicate
        await Assert.That(stored[0].LastUsedIp).IsEqualTo("192.168.99.99");
        await Assert.That(stored[0].ExpiresAt).IsNotNull();
    }

    // ───────── CR-26: DiscussionReadStateRepositoryAdapter ─────────

    [Test]
    public async Task DiscussionReadState_SaveUpdate_PersistsAcrossContexts()
    {
        // First save inserts (works via Add). Second save mutates the existing row —
        // pre-fix that silently no-op'd, leaving the unread indicator stuck.
        const string userId = "user-readstate";
        const string discussionId = "disc-readstate";

        using (var initialCtx = _db.CreateSeparateContext())
        {
            var repo = new DiscussionReadStateRepositoryAdapter(initialCtx, _hybridCache);
            await repo.SaveAsync(DiscussionReadState.Rehydrate(
                UserId.From(userId),
                DiscussionId.From(discussionId),
                PostId.From("post-1"),
                DateTime.UtcNow.AddMinutes(-10)));
        }

        var updatedAt = DateTime.UtcNow;
        using (var updateCtx = _db.CreateSeparateContext())
        {
            var repo = new DiscussionReadStateRepositoryAdapter(updateCtx, _hybridCache);
            await repo.SaveAsync(DiscussionReadState.Rehydrate(
                UserId.From(userId),
                DiscussionId.From(discussionId),
                PostId.From("post-42"),
                updatedAt));
        }

        using var verifyCtx = _db.CreateSeparateContext();
        var stored = await verifyCtx.DiscussionReadStates
            .SingleAsync(rs => rs.UserId == userId && rs.DiscussionId == discussionId);
        await Assert.That(stored.LastReadPostId).IsEqualTo("post-42");
        // SQLite drops sub-second precision; compare to whole-second.
        await Assert.That(stored.LastReadAt.ToString("O")[..19]).IsEqualTo(updatedAt.ToString("O")[..19]);
    }

    // ───────── CR-27: SessionManagementService ─────────

    [Test]
    public async Task SessionManagement_RevokeAllExcept_PersistsAcrossContexts()
    {
        var user = await CreateUserAsync();

        // Seed three refresh tokens; one will be the "current" session preserved.
        for (var i = 0; i < 3; i++)
        {
            _db.Context.RefreshTokens.Add(new RefreshTokenDatabaseEntity
            {
                PublicId = $"session-{i}",
                UserId = user.Id,
                TokenValue = $"token-hash-{i}",
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(30)
            });
        }
        await _db.Context.SaveChangesAsync();

        var jwt = Substitute.For<IJwtTokenService>();
        var logger = NullLogger<SessionManagementService>.Instance;

        int revoked;
        using (var serviceContext = _db.CreateSeparateContext())
        {
            var service = new SessionManagementService(serviceContext, jwt, logger);
            revoked = await service.RevokeAllExceptAsync(user.PublicId!, excludeSessionId: "session-0");
        }

        await Assert.That(revoked).IsEqualTo(2);

        using var verifyContext = _db.CreateSeparateContext();
        var stored = await verifyContext.RefreshTokens.OrderBy(t => t.PublicId).ToListAsync();
        await Assert.That(stored.Count).IsEqualTo(3);
        await Assert.That(stored[0].PublicId).IsEqualTo("session-0");
        await Assert.That(stored[0].RevokedAt).IsNull();
        await Assert.That(stored[1].RevokedAt).IsNotNull();
        await Assert.That(stored[2].RevokedAt).IsNotNull();
    }

    // ───────── CR-28: RuleService.UpdateSiteRulesAsync (site scope) ─────────

    [Test]
    public async Task RuleService_UpdateSiteRules_BumpsRevisionAcrossContexts()
    {
        // Seed an existing site-rules-revision setting; the bug specifically affected
        // the UPDATE path (the first Add() worked, every subsequent update no-op'd).
        _db.Context.SystemSettings.Add(new SystemSettingDatabaseEntity
        {
            PublicId = Ulid.NewUlid().ToString(),
            Category = "Rules",
            Key = "SiteRulesRevision",
            Value = "00000000",
            ValueType = "String",
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        });
        await _db.Context.SaveChangesAsync();

        var services = new ServiceCollection();
        services.AddMemoryCache();
        services.AddHybridCache();
        await using var provider = services.BuildServiceProvider();
        var hybridCache = provider.GetRequiredService<HybridCache>();

        using (var serviceContext = _db.CreateSeparateContext())
        {
            var ruleService = new RuleService(serviceContext, hybridCache);
            await ruleService.UpdateRulesAsync(
                "Site", null,
                new UpdateRulesRequest { Rules = [new RuleDto { Title = "Rule 1", Description = "Body 1" }] },
                CancellationToken.None);
        }

        using var verifyContext = _db.CreateSeparateContext();
        var revision = await verifyContext.SystemSettings
            .SingleAsync(s => s.Category == "Rules" && s.Key == "SiteRulesRevision");
        await Assert.That(revision.Value).IsNotEqualTo("00000000");
        await Assert.That(revision.Value!.Length).IsEqualTo(8); // Guid.NewGuid().ToString("N")[..8]
    }
}
