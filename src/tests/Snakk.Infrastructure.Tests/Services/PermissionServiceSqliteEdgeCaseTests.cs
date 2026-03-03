using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Snakk.Application.Services;
using Snakk.Infrastructure.Services;
using Snakk.Infrastructure.Tests.Helpers;
using Snakk.Shared.Enums;

namespace Snakk.Infrastructure.Tests.Services;

/// <summary>
/// Edge case tests for PermissionService using a real SQLite in-memory database.
/// Covers multi-role scenarios, cache invalidation, soft deletes, and service methods.
/// </summary>
public class PermissionServiceSqliteEdgeCaseTests : IDisposable
{
    private readonly SqliteTestDatabase _db;
    private readonly TestDataBuilder _builder;
    private readonly ServiceProvider _cacheServiceProvider;
    private readonly PermissionService _service;

    public PermissionServiceSqliteEdgeCaseTests()
    {
        _db = new SqliteTestDatabase();
        _builder = new TestDataBuilder(_db.Context);
        var services = new ServiceCollection();
        services.AddHybridCache();
        _cacheServiceProvider = services.BuildServiceProvider();
        var cache = _cacheServiceProvider.GetRequiredService<HybridCache>();
        _service = new PermissionService(
            _db.Context,
            cache,
            Mock.Of<ILogger<PermissionService>>(),
            Mock.Of<ISecurityService>());
    }

    public void Dispose()
    {
        _cacheServiceProvider.Dispose();
        _db.Dispose();
    }

    #region Multi-Role Scenarios

    [Test]
    public async Task MultipleRoles_DifferentScopes()
    {
        // Arrange - user has SpaceMod on Space A + HubMod on Hub B
        var user = await _builder.CreateUserAsync();
        var admin = await _builder.CreateUserAsync("Admin");
        var community = await _builder.CreateCommunityAsync();

        var hubA = await _builder.CreateHubAsync(community.Id, "Hub A");
        var spaceA = await _builder.CreateSpaceAsync(hubA.Id, "Space A");

        var hubB = await _builder.CreateHubAsync(community.Id, "Hub B");
        var spaceB = await _builder.CreateSpaceAsync(hubB.Id, "Space B");

        await _builder.AssignRoleAsync(user.Id, UserRoleTypeEnum.SpaceMod, admin.Id, spaceId: spaceA.Id);
        await _builder.AssignRoleAsync(user.Id, UserRoleTypeEnum.HubMod, admin.Id, hubId: hubB.Id);

        // Act & Assert
        // SpaceMod grants access to Space A
        var spaceAAccess = await _service.UserHasPermissionAsync(user.PublicId, "Moderate", "space", spaceA.PublicId);
        await Assert.That(spaceAAccess).IsTrue();

        // HubMod grants access to Hub B
        var hubBAccess = await _service.UserHasPermissionAsync(user.PublicId, "Moderate", "hub", hubB.PublicId);
        await Assert.That(hubBAccess).IsTrue();

        // HubMod on Hub B also grants access to Space B (child of Hub B)
        var spaceBAccess = await _service.UserHasPermissionAsync(user.PublicId, "Moderate", "space", spaceB.PublicId);
        await Assert.That(spaceBAccess).IsTrue();

        // SpaceMod on Space A does NOT grant access to Hub A (no upward bubble)
        var hubAAccess = await _service.UserHasPermissionAsync(user.PublicId, "Moderate", "hub", hubA.PublicId);
        await Assert.That(hubAAccess).IsFalse();
    }

    [Test]
    public async Task HigherRole_TakesPrecedence()
    {
        // Arrange - user has CommunityAdmin + SpaceMod
        var user = await _builder.CreateUserAsync();
        var admin = await _builder.CreateUserAsync("Admin");
        var community = await _builder.CreateCommunityAsync();
        var hub = await _builder.CreateHubAsync(community.Id);
        var space = await _builder.CreateSpaceAsync(hub.Id);

        await _builder.AssignRoleAsync(user.Id, UserRoleTypeEnum.CommunityAdmin, admin.Id, communityId: community.Id);
        await _builder.AssignRoleAsync(user.Id, UserRoleTypeEnum.SpaceMod, admin.Id, spaceId: space.Id);

        // Act - CommunityAdmin gives community-level access (higher than SpaceMod)
        var communityAccess = await _service.UserHasPermissionAsync(user.PublicId, "Moderate", "community", community.PublicId);
        var hubAccess = await _service.UserHasPermissionAsync(user.PublicId, "Moderate", "hub", hub.PublicId);
        var spaceAccess = await _service.UserHasPermissionAsync(user.PublicId, "Moderate", "space", space.PublicId);

        // Assert
        await Assert.That(communityAccess).IsTrue();
        await Assert.That(hubAccess).IsTrue();
        await Assert.That(spaceAccess).IsTrue();
    }

    [Test]
    public async Task RevokedRole_ActiveRole_OnlyActiveConsidered()
    {
        // Arrange - one revoked CommunityAdmin + one active SpaceMod
        var user = await _builder.CreateUserAsync();
        var admin = await _builder.CreateUserAsync("Admin");
        var community = await _builder.CreateCommunityAsync();
        var hub = await _builder.CreateHubAsync(community.Id);
        var space = await _builder.CreateSpaceAsync(hub.Id);

        // Revoked CommunityAdmin
        var revokedRole = await _builder.AssignRoleAsync(user.Id, UserRoleTypeEnum.CommunityAdmin, admin.Id, communityId: community.Id);
        revokedRole.RevokedAt = DateTime.UtcNow;
        revokedRole.RevokedByUserId = admin.Id;
        await _db.Context.SaveChangesAsync();

        // Active SpaceMod
        await _builder.AssignRoleAsync(user.Id, UserRoleTypeEnum.SpaceMod, admin.Id, spaceId: space.Id);

        // Act & Assert
        // No community access (CommunityAdmin was revoked)
        var communityAccess = await _service.UserHasPermissionAsync(user.PublicId, "Moderate", "community", community.PublicId);
        await Assert.That(communityAccess).IsFalse();

        // Yes space access (SpaceMod is active)
        var spaceAccess = await _service.UserHasPermissionAsync(user.PublicId, "Moderate", "space", space.PublicId);
        await Assert.That(spaceAccess).IsTrue();
    }

    [Test]
    public async Task CommunityMod_AccessesHub()
    {
        // Arrange - CommunityMod (not just CommunityAdmin) should access child hubs
        var user = await _builder.CreateUserAsync();
        var admin = await _builder.CreateUserAsync("Admin");
        var community = await _builder.CreateCommunityAsync();
        var hub = await _builder.CreateHubAsync(community.Id);

        await _builder.AssignRoleAsync(user.Id, UserRoleTypeEnum.CommunityMod, admin.Id, communityId: community.Id);

        // Act
        var result = await _service.UserHasPermissionAsync(user.PublicId, "Moderate", "hub", hub.PublicId);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task DeeplyNestedHierarchy_HubModAccessCorrectSpacesOnly()
    {
        // Arrange - community with 2 hubs, each with 2 spaces
        var user = await _builder.CreateUserAsync();
        var admin = await _builder.CreateUserAsync("Admin");
        var community = await _builder.CreateCommunityAsync();

        var hub1 = await _builder.CreateHubAsync(community.Id, "Hub 1");
        var space1a = await _builder.CreateSpaceAsync(hub1.Id, "Space 1A");
        var space1b = await _builder.CreateSpaceAsync(hub1.Id, "Space 1B");

        var hub2 = await _builder.CreateHubAsync(community.Id, "Hub 2");
        var space2a = await _builder.CreateSpaceAsync(hub2.Id, "Space 2A");
        var space2b = await _builder.CreateSpaceAsync(hub2.Id, "Space 2B");

        // HubMod for hub1 only
        await _builder.AssignRoleAsync(user.Id, UserRoleTypeEnum.HubMod, admin.Id, hubId: hub1.Id);

        // Act & Assert - can access hub1's spaces
        var space1aAccess = await _service.UserHasPermissionAsync(user.PublicId, "Moderate", "space", space1a.PublicId);
        await Assert.That(space1aAccess).IsTrue();

        var space1bAccess = await _service.UserHasPermissionAsync(user.PublicId, "Moderate", "space", space1b.PublicId);
        await Assert.That(space1bAccess).IsTrue();

        // Cannot access hub2's spaces
        var space2aAccess = await _service.UserHasPermissionAsync(user.PublicId, "Moderate", "space", space2a.PublicId);
        await Assert.That(space2aAccess).IsFalse();

        var space2bAccess = await _service.UserHasPermissionAsync(user.PublicId, "Moderate", "space", space2b.PublicId);
        await Assert.That(space2bAccess).IsFalse();
    }

    [Test]
    public async Task UserWithNoRoles_DeniedEverywhere()
    {
        // Arrange - user exists but has no roles at all
        var user = await _builder.CreateUserAsync();
        var community = await _builder.CreateCommunityAsync();
        var hub = await _builder.CreateHubAsync(community.Id);
        var space = await _builder.CreateSpaceAsync(hub.Id);

        // Act & Assert
        var communityAccess = await _service.UserHasPermissionAsync(user.PublicId, "Moderate", "community", community.PublicId);
        await Assert.That(communityAccess).IsFalse();

        var hubAccess = await _service.UserHasPermissionAsync(user.PublicId, "Moderate", "hub", hub.PublicId);
        await Assert.That(hubAccess).IsFalse();

        var spaceAccess = await _service.UserHasPermissionAsync(user.PublicId, "Moderate", "space", space.PublicId);
        await Assert.That(spaceAccess).IsFalse();

        var noScopeAccess = await _service.UserHasPermissionAsync(user.PublicId, "Moderate");
        await Assert.That(noScopeAccess).IsFalse();
    }

    #endregion

    #region Service Method Tests

    [Test]
    public async Task AssignPermissionToRole_PersistsInDb()
    {
        // Arrange
        var admin = await _builder.CreateUserAsync("Admin");
        await _builder.AssignRoleAsync(admin.Id, UserRoleTypeEnum.GlobalAdmin, admin.Id);

        var targetUser = await _builder.CreateUserAsync("TargetUser");
        var role = await _builder.AssignRoleAsync(targetUser.Id, UserRoleTypeEnum.CommunityMod, admin.Id);

        var permission = await _builder.CreatePermissionAsync("ManageReports", "Moderation");

        // Act
        await _service.AssignPermissionToRoleAsync(role.Id, permission.Id, admin.PublicId);

        // Assert - verify in database
        var rp = await _db.Context.RolePermissions
            .FirstOrDefaultAsync(rp => rp.RoleId == role.Id && rp.PermissionId == permission.Id);
        await Assert.That(rp).IsNotNull();
        await Assert.That(rp!.GrantedById).IsEqualTo(admin.Id);
    }

    [Test]
    public async Task RevokePermissionFromRole_RemovesFromDb()
    {
        // Arrange
        var admin = await _builder.CreateUserAsync("Admin");
        var user = await _builder.CreateUserAsync("TargetUser");
        var role = await _builder.AssignRoleAsync(user.Id, UserRoleTypeEnum.CommunityMod, admin.Id);
        var permission = await _builder.CreatePermissionAsync("ManageReports", "Moderation");

        // First assign the permission
        await _builder.AssignPermissionToRoleAsync(role.Id, permission.Id, admin.Id);

        // Verify it exists
        var existsBefore = await _db.Context.RolePermissions
            .AnyAsync(rp => rp.RoleId == role.Id && rp.PermissionId == permission.Id);
        await Assert.That(existsBefore).IsTrue();

        // Act - revoke it
        await _service.RevokePermissionFromRoleAsync(role.Id, permission.Id, admin.PublicId);

        // Assert - verify it's gone
        var existsAfter = await _db.Context.RolePermissions
            .AnyAsync(rp => rp.RoleId == role.Id && rp.PermissionId == permission.Id);
        await Assert.That(existsAfter).IsFalse();
    }

    [Test]
    public async Task GrantTemporaryRole_PersistsInDb()
    {
        // Arrange
        var admin = await _builder.CreateUserAsync("Admin");
        await _builder.AssignRoleAsync(admin.Id, UserRoleTypeEnum.GlobalAdmin, admin.Id);

        var targetUser = await _builder.CreateUserAsync("TargetUser");
        var community = await _builder.CreateCommunityAsync();
        var hub = await _builder.CreateHubAsync(community.Id);
        var expiresAt = DateTime.UtcNow.AddHours(4);

        // Act
        var result = await _service.GrantTemporaryRoleAsync(
            targetUser.PublicId, "HubMod", "Hub", hub.Id, expiresAt, "Emergency coverage", admin.PublicId);

        // Assert - verify entity was created in DB
        var elevation = await _db.Context.TemporaryRoleElevations
            .FirstOrDefaultAsync(e => e.PublicId == result.PublicId);
        await Assert.That(elevation).IsNotNull();
        await Assert.That(elevation!.RoleType).IsEqualTo("HubMod");
        await Assert.That(elevation.Scope).IsEqualTo("Hub");
        await Assert.That(elevation.ScopeId).IsEqualTo(hub.Id);
        await Assert.That(elevation.UserId).IsEqualTo(targetUser.Id);
        await Assert.That(elevation.GrantedById).IsEqualTo(admin.Id);
        await Assert.That(elevation.RevokedAt).IsNull();
    }

    [Test]
    public async Task RevokeTemporaryRole_SetsRevokedFields()
    {
        // Arrange
        var admin = await _builder.CreateUserAsync("Admin");
        var targetUser = await _builder.CreateUserAsync("TargetUser");
        var community = await _builder.CreateCommunityAsync();
        var hub = await _builder.CreateHubAsync(community.Id);

        var elevation = await _builder.CreateTemporaryElevationAsync(
            targetUser.Id,
            "HubMod",
            "Hub",
            hub.Id,
            admin.Id,
            expiresAt: DateTime.UtcNow.AddHours(4));

        // Act
        await _service.RevokeTemporaryRoleAsync(elevation.PublicId, "No longer needed", admin.PublicId);

        // Assert - reload from DB to verify
        var revoked = await _db.Context.TemporaryRoleElevations
            .FirstAsync(e => e.PublicId == elevation.PublicId);
        await Assert.That(revoked.RevokedAt).IsNotNull();
        await Assert.That(revoked.RevokedById).IsEqualTo(admin.Id);
        await Assert.That(revoked.RevokedReason).IsEqualTo("No longer needed");
    }

    [Test]
    public async Task GetRolePermissions_ReturnsCorrect()
    {
        // Arrange
        var admin = await _builder.CreateUserAsync("Admin");
        var user = await _builder.CreateUserAsync("User");
        var role = await _builder.AssignRoleAsync(user.Id, UserRoleTypeEnum.CommunityMod, admin.Id);

        var perm1 = await _builder.CreatePermissionAsync("ManageReports", "Moderation");
        var perm2 = await _builder.CreatePermissionAsync("ManageUsers", "Admin");
        await _builder.AssignPermissionToRoleAsync(role.Id, perm1.Id, admin.Id);
        await _builder.AssignPermissionToRoleAsync(role.Id, perm2.Id, admin.Id);

        // Act
        var result = await _service.GetRolePermissionsAsync(role.Id);

        // Assert
        await Assert.That(result.Count).IsEqualTo(2);
        await Assert.That(result.Any(p => p.Name == "ManageReports")).IsTrue();
        await Assert.That(result.Any(p => p.Name == "ManageUsers")).IsTrue();
    }

    [Test]
    public async Task GetAllPermissions_ReturnsAll()
    {
        // Arrange
        await _builder.CreatePermissionAsync("ManageReports", "Moderation");
        await _builder.CreatePermissionAsync("ManageUsers", "Admin");
        await _builder.CreatePermissionAsync("ViewAnalytics", "Analytics");

        // Act
        var result = await _service.GetAllPermissionsAsync();

        // Assert
        await Assert.That(result.Count).IsEqualTo(3);
    }

    #endregion

    #region Cache and Soft Delete Tests

    [Test]
    public async Task CacheInvalidation_AfterPermissionAssignment()
    {
        // Arrange
        var admin = await _builder.CreateUserAsync("Admin");
        var user = await _builder.CreateUserAsync("User");
        var role = await _builder.AssignRoleAsync(user.Id, UserRoleTypeEnum.CommunityMod, admin.Id);
        var permission = await _builder.CreatePermissionAsync("ManageReports", "Moderation");

        // First check - should be false (permission not assigned yet)
        var resultBefore = await _service.UserHasPermissionAsync(user.PublicId, "ManageReports");
        await Assert.That(resultBefore).IsFalse();

        // Act - assign permission via the service (which invalidates cache)
        await _service.AssignPermissionToRoleAsync(role.Id, permission.Id, admin.PublicId);

        // Assert - should now return true since cache was invalidated
        var resultAfter = await _service.UserHasPermissionAsync(user.PublicId, "ManageReports");
        await Assert.That(resultAfter).IsTrue();
    }

    [Test]
    public async Task SoftDeletedUser_DeniedAccess()
    {
        // Arrange - create user, assign GlobalAdmin, then soft-delete
        var user = await _builder.CreateUserAsync();
        await _builder.AssignRoleAsync(user.Id, UserRoleTypeEnum.GlobalAdmin, user.Id);

        // Soft-delete the user (query filter on UserDatabaseEntity excludes IsDeleted=true)
        user.IsDeleted = true;
        user.DeletedAt = DateTime.UtcNow;
        await _db.Context.SaveChangesAsync();

        // Act - user lookup should fail due to query filter
        var result = await _service.UserHasPermissionAsync(user.PublicId, "Moderate", "community", "1");

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task RevokedTemporaryElevation_ThenNew_GrantsAccess()
    {
        // Arrange
        var user = await _builder.CreateUserAsync();
        var admin = await _builder.CreateUserAsync("Admin");
        var community = await _builder.CreateCommunityAsync();
        var hub = await _builder.CreateHubAsync(community.Id);

        // Create first elevation and revoke it
        var firstElevation = await _builder.CreateTemporaryElevationAsync(
            user.Id, "HubMod", "Hub", hub.Id, admin.Id,
            expiresAt: DateTime.UtcNow.AddHours(4));

        firstElevation.RevokedAt = DateTime.UtcNow;
        firstElevation.RevokedById = admin.Id;
        firstElevation.RevokedReason = "Revoked for testing";
        await _db.Context.SaveChangesAsync();

        // Verify access is denied after revocation
        var accessAfterRevoke = await _service.UserHasPermissionAsync(user.PublicId, "Moderate", "hub", hub.PublicId);
        await Assert.That(accessAfterRevoke).IsFalse();

        // Create a new elevation
        await _builder.CreateTemporaryElevationAsync(
            user.Id, "HubMod", "Hub", hub.Id, admin.Id,
            expiresAt: DateTime.UtcNow.AddHours(4));

        // Act - new elevation should grant access
        var result = await _service.UserHasPermissionAsync(user.PublicId, "Moderate", "hub", hub.PublicId);

        // Assert
        await Assert.That(result).IsTrue();
    }

    #endregion
}
