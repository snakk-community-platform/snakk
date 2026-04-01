using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Snakk.Application.Services;
using Snakk.Infrastructure.Services;
using Snakk.Infrastructure.Tests.Helpers;
using Snakk.Shared.Enums;

namespace Snakk.Infrastructure.Tests.Services;

/// <summary>
/// Tests for PermissionService using a real SQLite in-memory database
/// to validate hierarchical permission checks against actual FK relationships.
/// </summary>
public class PermissionServiceSqliteTests : IDisposable
{
    private readonly SqliteTestDatabase _db;
    private readonly TestDataBuilder _builder;
    private readonly ServiceProvider _cacheServiceProvider;
    private readonly PermissionService _service;

    public PermissionServiceSqliteTests()
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
            Substitute.For<ILogger<PermissionService>>(),
            Substitute.For<ISecurityService>());
    }

    public void Dispose()
    {
        _cacheServiceProvider.Dispose();
        _db.Dispose();
    }

    #region GlobalAdmin Tests (4 tests)

    [Test]
    public async Task GlobalAdmin_HasAccessToAnyScopeAndPermission()
    {
        // Arrange
        var user = await _builder.CreateUserAsync();
        var community = await _builder.CreateCommunityAsync();
        await _builder.AssignRoleAsync(user.Id, UserRoleTypeEnum.GlobalAdmin, user.Id);

        // Act
        var result = await _service.UserHasPermissionAsync(user.PublicId, "AnyPermission", "community", community.PublicId);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task GlobalAdmin_HasAccessWithoutScope()
    {
        // Arrange
        var user = await _builder.CreateUserAsync();
        await _builder.AssignRoleAsync(user.Id, UserRoleTypeEnum.GlobalAdmin, user.Id);

        // Act
        var result = await _service.UserHasPermissionAsync(user.PublicId, "AnyPermission");

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task GlobalAdmin_HasAccessToDeepNestedEntities()
    {
        // Arrange
        var (user, community, hub, space, discussion, post) = await _builder.CreateFullHierarchyAsync();
        var admin = await _builder.CreateUserAsync("GlobalAdminUser");
        await _builder.AssignRoleAsync(admin.Id, UserRoleTypeEnum.GlobalAdmin, admin.Id);

        // Act & Assert - verify access at every level of the hierarchy
        var communityAccess = await _service.UserHasPermissionAsync(admin.PublicId, "Moderate", "community", community.PublicId);
        await Assert.That(communityAccess).IsTrue();

        var hubAccess = await _service.UserHasPermissionAsync(admin.PublicId, "Moderate", "hub", hub.PublicId);
        await Assert.That(hubAccess).IsTrue();

        var spaceAccess = await _service.UserHasPermissionAsync(admin.PublicId, "Moderate", "space", space.PublicId);
        await Assert.That(spaceAccess).IsTrue();

        var discussionAccess = await _service.UserHasPermissionAsync(admin.PublicId, "Moderate", "discussion", discussion.PublicId);
        await Assert.That(discussionAccess).IsTrue();
    }

    [Test]
    public async Task GlobalAdmin_MultipleRoles_StillWorks()
    {
        // Arrange - user has GlobalAdmin + SpaceMod on an unrelated space
        var (user, community, hub, space, _, _) = await _builder.CreateFullHierarchyAsync();
        var admin = await _builder.CreateUserAsync("MultiRoleAdmin");
        await _builder.AssignRoleAsync(admin.Id, UserRoleTypeEnum.GlobalAdmin, admin.Id);
        await _builder.AssignRoleAsync(admin.Id, UserRoleTypeEnum.SpaceMod, admin.Id, spaceId: space.Id);

        // Create a completely separate hierarchy
        var otherCommunity = await _builder.CreateCommunityAsync("Other Community");
        var otherHub = await _builder.CreateHubAsync(otherCommunity.Id);
        var otherSpace = await _builder.CreateSpaceAsync(otherHub.Id);

        // Act - GlobalAdmin should still grant access to the other community's space
        var result = await _service.UserHasPermissionAsync(admin.PublicId, "Moderate", "space", otherSpace.PublicId);

        // Assert
        await Assert.That(result).IsTrue();
    }

    #endregion

    #region Community Hierarchy Tests (5 tests)

    [Test]
    public async Task CommunityAdmin_HasAccessToCommunity()
    {
        // Arrange
        var user = await _builder.CreateUserAsync();
        var community = await _builder.CreateCommunityAsync();
        await _builder.AssignRoleAsync(user.Id, UserRoleTypeEnum.CommunityAdmin, user.Id, communityId: community.Id);

        // Act
        var result = await _service.UserHasPermissionAsync(user.PublicId, "Moderate", "community", community.PublicId);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task CommunityAdmin_HasAccessToChildHub()
    {
        // Arrange
        var user = await _builder.CreateUserAsync();
        var community = await _builder.CreateCommunityAsync();
        var hub = await _builder.CreateHubAsync(community.Id);
        await _builder.AssignRoleAsync(user.Id, UserRoleTypeEnum.CommunityAdmin, user.Id, communityId: community.Id);

        // Act - hub scope resolves Hub.CommunityId FK to find parent community
        var result = await _service.UserHasPermissionAsync(user.PublicId, "Moderate", "hub", hub.PublicId);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task CommunityAdmin_HasAccessToGrandchildSpace()
    {
        // Arrange
        var user = await _builder.CreateUserAsync();
        var community = await _builder.CreateCommunityAsync();
        var hub = await _builder.CreateHubAsync(community.Id);
        var space = await _builder.CreateSpaceAsync(hub.Id);
        await _builder.AssignRoleAsync(user.Id, UserRoleTypeEnum.CommunityAdmin, user.Id, communityId: community.Id);

        // Act - space scope resolves Space->Hub->Community chain
        var result = await _service.UserHasPermissionAsync(user.PublicId, "Moderate", "space", space.PublicId);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task CommunityAdmin_HasAccessToDiscussion()
    {
        // Arrange
        var user = await _builder.CreateUserAsync();
        var community = await _builder.CreateCommunityAsync();
        var hub = await _builder.CreateHubAsync(community.Id);
        var space = await _builder.CreateSpaceAsync(hub.Id);
        var discussion = await _builder.CreateDiscussionAsync(space.Id, user.Id);
        await _builder.AssignRoleAsync(user.Id, UserRoleTypeEnum.CommunityAdmin, user.Id, communityId: community.Id);

        // Act - discussion scope resolves Discussion->Space->Hub->Community chain
        var result = await _service.UserHasPermissionAsync(user.PublicId, "Moderate", "discussion", discussion.PublicId);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task CommunityMod_HasSameHierarchicalAccess()
    {
        // Arrange - CommunityMod (not Admin) should also get hierarchical access
        var user = await _builder.CreateUserAsync();
        var community = await _builder.CreateCommunityAsync();
        var hub = await _builder.CreateHubAsync(community.Id);
        var space = await _builder.CreateSpaceAsync(hub.Id);
        var discussion = await _builder.CreateDiscussionAsync(space.Id, user.Id);
        await _builder.AssignRoleAsync(user.Id, UserRoleTypeEnum.CommunityMod, user.Id, communityId: community.Id);

        // Act & Assert - CommunityMod access at every level
        var communityAccess = await _service.UserHasPermissionAsync(user.PublicId, "Moderate", "community", community.PublicId);
        await Assert.That(communityAccess).IsTrue();

        var hubAccess = await _service.UserHasPermissionAsync(user.PublicId, "Moderate", "hub", hub.PublicId);
        await Assert.That(hubAccess).IsTrue();

        var spaceAccess = await _service.UserHasPermissionAsync(user.PublicId, "Moderate", "space", space.PublicId);
        await Assert.That(spaceAccess).IsTrue();

        var discussionAccess = await _service.UserHasPermissionAsync(user.PublicId, "Moderate", "discussion", discussion.PublicId);
        await Assert.That(discussionAccess).IsTrue();
    }

    #endregion

    #region Hub Hierarchy Tests (4 tests)

    [Test]
    public async Task HubMod_HasAccessToHub()
    {
        // Arrange
        var user = await _builder.CreateUserAsync();
        var community = await _builder.CreateCommunityAsync();
        var hub = await _builder.CreateHubAsync(community.Id);
        await _builder.AssignRoleAsync(user.Id, UserRoleTypeEnum.HubMod, user.Id, hubId: hub.Id);

        // Act
        var result = await _service.UserHasPermissionAsync(user.PublicId, "Moderate", "hub", hub.PublicId);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task HubMod_HasAccessToChildSpace()
    {
        // Arrange
        var user = await _builder.CreateUserAsync();
        var community = await _builder.CreateCommunityAsync();
        var hub = await _builder.CreateHubAsync(community.Id);
        var space = await _builder.CreateSpaceAsync(hub.Id);
        await _builder.AssignRoleAsync(user.Id, UserRoleTypeEnum.HubMod, user.Id, hubId: hub.Id);

        // Act - space scope resolves Space.HubId FK
        var result = await _service.UserHasPermissionAsync(user.PublicId, "Moderate", "space", space.PublicId);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task HubMod_HasAccessToDiscussionInSpace()
    {
        // Arrange
        var user = await _builder.CreateUserAsync();
        var community = await _builder.CreateCommunityAsync();
        var hub = await _builder.CreateHubAsync(community.Id);
        var space = await _builder.CreateSpaceAsync(hub.Id);
        var discussion = await _builder.CreateDiscussionAsync(space.Id, user.Id);
        await _builder.AssignRoleAsync(user.Id, UserRoleTypeEnum.HubMod, user.Id, hubId: hub.Id);

        // Act - discussion->space->hub chain
        var result = await _service.UserHasPermissionAsync(user.PublicId, "Moderate", "discussion", discussion.PublicId);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task HubMod_NoAccessToOtherHub()
    {
        // Arrange
        var user = await _builder.CreateUserAsync();
        var community = await _builder.CreateCommunityAsync();
        var hub1 = await _builder.CreateHubAsync(community.Id, "Hub 1");
        var hub2 = await _builder.CreateHubAsync(community.Id, "Hub 2");
        await _builder.AssignRoleAsync(user.Id, UserRoleTypeEnum.HubMod, user.Id, hubId: hub1.Id);

        // Act - user is HubMod for hub1, not hub2
        var result = await _service.UserHasPermissionAsync(user.PublicId, "Moderate", "hub", hub2.PublicId);

        // Assert
        await Assert.That(result).IsFalse();
    }

    #endregion

    #region Space Scope Tests (4 tests)

    [Test]
    public async Task SpaceMod_HasAccessToSpace()
    {
        // Arrange
        var user = await _builder.CreateUserAsync();
        var community = await _builder.CreateCommunityAsync();
        var hub = await _builder.CreateHubAsync(community.Id);
        var space = await _builder.CreateSpaceAsync(hub.Id);
        await _builder.AssignRoleAsync(user.Id, UserRoleTypeEnum.SpaceMod, user.Id, spaceId: space.Id);

        // Act
        var result = await _service.UserHasPermissionAsync(user.PublicId, "Moderate", "space", space.PublicId);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task SpaceMod_HasAccessToDiscussionInSpace()
    {
        // Arrange
        var user = await _builder.CreateUserAsync();
        var community = await _builder.CreateCommunityAsync();
        var hub = await _builder.CreateHubAsync(community.Id);
        var space = await _builder.CreateSpaceAsync(hub.Id);
        var discussion = await _builder.CreateDiscussionAsync(space.Id, user.Id);
        await _builder.AssignRoleAsync(user.Id, UserRoleTypeEnum.SpaceMod, user.Id, spaceId: space.Id);

        // Act
        var result = await _service.UserHasPermissionAsync(user.PublicId, "Moderate", "discussion", discussion.PublicId);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task SpaceMod_NoAccessToParentHub()
    {
        // Arrange - SpaceMod should NOT have access to the parent hub
        var user = await _builder.CreateUserAsync();
        var community = await _builder.CreateCommunityAsync();
        var hub = await _builder.CreateHubAsync(community.Id);
        var space = await _builder.CreateSpaceAsync(hub.Id);
        await _builder.AssignRoleAsync(user.Id, UserRoleTypeEnum.SpaceMod, user.Id, spaceId: space.Id);

        // Act - permissions do NOT bubble up
        var result = await _service.UserHasPermissionAsync(user.PublicId, "Moderate", "hub", hub.PublicId);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task SpaceMod_NoAccessToOtherSpace()
    {
        // Arrange
        var user = await _builder.CreateUserAsync();
        var community = await _builder.CreateCommunityAsync();
        var hub = await _builder.CreateHubAsync(community.Id);
        var space1 = await _builder.CreateSpaceAsync(hub.Id, "Space 1");
        var space2 = await _builder.CreateSpaceAsync(hub.Id, "Space 2");
        await _builder.AssignRoleAsync(user.Id, UserRoleTypeEnum.SpaceMod, user.Id, spaceId: space1.Id);

        // Act
        var result = await _service.UserHasPermissionAsync(user.PublicId, "Moderate", "space", space2.PublicId);

        // Assert
        await Assert.That(result).IsFalse();
    }

    #endregion

    #region Post Scope Tests (3 tests)

    [Test]
    public async Task SpaceMod_HasAccessToPostViaFullChain()
    {
        // Arrange
        var user = await _builder.CreateUserAsync();
        var community = await _builder.CreateCommunityAsync();
        var hub = await _builder.CreateHubAsync(community.Id);
        var space = await _builder.CreateSpaceAsync(hub.Id);
        var discussion = await _builder.CreateDiscussionAsync(space.Id, user.Id);
        var post = await _builder.CreatePostAsync(discussion.Id, user.Id);
        await _builder.AssignRoleAsync(user.Id, UserRoleTypeEnum.SpaceMod, user.Id, spaceId: space.Id);

        // Act - post->discussion->space chain
        var result = await _service.UserHasPermissionAsync(user.PublicId, "Moderate", "post", post.PublicId);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task HubMod_HasAccessToPostViaFullChain()
    {
        // Arrange
        var user = await _builder.CreateUserAsync();
        var community = await _builder.CreateCommunityAsync();
        var hub = await _builder.CreateHubAsync(community.Id);
        var space = await _builder.CreateSpaceAsync(hub.Id);
        var discussion = await _builder.CreateDiscussionAsync(space.Id, user.Id);
        var post = await _builder.CreatePostAsync(discussion.Id, user.Id);
        await _builder.AssignRoleAsync(user.Id, UserRoleTypeEnum.HubMod, user.Id, hubId: hub.Id);

        // Act - post->discussion->space->hub chain
        var result = await _service.UserHasPermissionAsync(user.PublicId, "Moderate", "post", post.PublicId);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task CommunityAdmin_HasAccessToPostViaFullChain()
    {
        // Arrange
        var user = await _builder.CreateUserAsync();
        var community = await _builder.CreateCommunityAsync();
        var hub = await _builder.CreateHubAsync(community.Id);
        var space = await _builder.CreateSpaceAsync(hub.Id);
        var discussion = await _builder.CreateDiscussionAsync(space.Id, user.Id);
        var post = await _builder.CreatePostAsync(discussion.Id, user.Id);
        await _builder.AssignRoleAsync(user.Id, UserRoleTypeEnum.CommunityAdmin, user.Id, communityId: community.Id);

        // Act - post->discussion->space->hub->community chain
        var result = await _service.UserHasPermissionAsync(user.PublicId, "Moderate", "post", post.PublicId);

        // Assert
        await Assert.That(result).IsTrue();
    }

    #endregion

    #region Negative Tests (4 tests)

    [Test]
    public async Task NonExistentUser_ReturnsFalse()
    {
        // Act - user PublicId does not exist in the database
        var result = await _service.UserHasPermissionAsync("nonexistent-user-id", "Moderate", "community", "1");

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task NonExistentHub_ReturnsFalse()
    {
        // Arrange - real user with HubMod role, but the hub ID in the scope check doesn't exist
        var user = await _builder.CreateUserAsync();
        var community = await _builder.CreateCommunityAsync();
        var hub = await _builder.CreateHubAsync(community.Id);
        await _builder.AssignRoleAsync(user.Id, UserRoleTypeEnum.HubMod, user.Id, hubId: hub.Id);

        // Act - querying a hub ID that doesn't exist in the database
        var result = await _service.UserHasPermissionAsync(user.PublicId, "Moderate", "hub", "99999");

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task NonExistentSpace_ReturnsFalse()
    {
        // Arrange
        var user = await _builder.CreateUserAsync();
        var community = await _builder.CreateCommunityAsync();
        var hub = await _builder.CreateHubAsync(community.Id);
        var space = await _builder.CreateSpaceAsync(hub.Id);
        await _builder.AssignRoleAsync(user.Id, UserRoleTypeEnum.SpaceMod, user.Id, spaceId: space.Id);

        // Act - querying a space ID that doesn't exist
        var result = await _service.UserHasPermissionAsync(user.PublicId, "Moderate", "space", "99999");

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task UnknownScope_ReturnsFalse()
    {
        // Arrange
        var user = await _builder.CreateUserAsync();
        await _builder.AssignRoleAsync(user.Id, UserRoleTypeEnum.GlobalAdmin, user.Id);

        // Note: GlobalAdmin check happens BEFORE scope check, so use a non-admin role
        var regularUser = await _builder.CreateUserAsync("RegularUser");
        var community = await _builder.CreateCommunityAsync();
        await _builder.AssignRoleAsync(regularUser.Id, UserRoleTypeEnum.CommunityAdmin, user.Id, communityId: community.Id);

        // Act - "invalid" is not a recognized scope
        var result = await _service.UserHasPermissionAsync(regularUser.PublicId, "Moderate", "invalid", community.PublicId);

        // Assert
        await Assert.That(result).IsFalse();
    }

    #endregion

    #region Temporary Elevation Tests (4 tests)

    [Test]
    public async Task TemporaryElevation_GrantsAccess_WhenActive()
    {
        // Arrange - user has no permanent roles, only a temporary HubMod elevation
        var user = await _builder.CreateUserAsync();
        var admin = await _builder.CreateUserAsync("TempAdmin");
        var community = await _builder.CreateCommunityAsync();
        var hub = await _builder.CreateHubAsync(community.Id);

        await _builder.CreateTemporaryElevationAsync(
            user.Id,
            "HubMod",
            "Hub",
            hub.Id,
            admin.Id,
            expiresAt: DateTime.UtcNow.AddHours(4),
            reason: "Emergency coverage");

        // Act
        var result = await _service.UserHasPermissionAsync(user.PublicId, "Moderate", "hub", hub.PublicId);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task TemporaryElevation_Expired_DeniesAccess()
    {
        // Arrange - elevation expired in the past
        var user = await _builder.CreateUserAsync();
        var admin = await _builder.CreateUserAsync("TempAdminExpired");
        var community = await _builder.CreateCommunityAsync();
        var hub = await _builder.CreateHubAsync(community.Id);

        await _builder.CreateTemporaryElevationAsync(
            user.Id,
            "HubMod",
            "Hub",
            hub.Id,
            admin.Id,
            expiresAt: DateTime.UtcNow.AddHours(-1)); // Expired 1 hour ago

        // Act
        var result = await _service.UserHasPermissionAsync(user.PublicId, "Moderate", "hub", hub.PublicId);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task TemporaryElevation_Revoked_DeniesAccess()
    {
        // Arrange - elevation was revoked
        var user = await _builder.CreateUserAsync();
        var admin = await _builder.CreateUserAsync("TempAdminRevoked");
        var community = await _builder.CreateCommunityAsync();
        var hub = await _builder.CreateHubAsync(community.Id);

        var elevation = await _builder.CreateTemporaryElevationAsync(
            user.Id,
            "HubMod",
            "Hub",
            hub.Id,
            admin.Id,
            expiresAt: DateTime.UtcNow.AddHours(4));

        // Revoke the elevation
        elevation.RevokedAt = DateTime.UtcNow;
        elevation.RevokedById = admin.Id;
        elevation.RevokedReason = "No longer needed";
        await _db.Context.SaveChangesAsync();

        // Act
        var result = await _service.UserHasPermissionAsync(user.PublicId, "Moderate", "hub", hub.PublicId);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task TemporaryElevation_CombinedWithPermanentRole()
    {
        // Arrange - user has permanent SpaceMod + temporary HubMod
        var user = await _builder.CreateUserAsync();
        var admin = await _builder.CreateUserAsync("TempAdminCombined");
        var community = await _builder.CreateCommunityAsync();
        var hub = await _builder.CreateHubAsync(community.Id);
        var space = await _builder.CreateSpaceAsync(hub.Id);

        // Permanent SpaceMod on the space
        await _builder.AssignRoleAsync(user.Id, UserRoleTypeEnum.SpaceMod, admin.Id, spaceId: space.Id);

        // Temporary HubMod on the hub
        await _builder.CreateTemporaryElevationAsync(
            user.Id,
            "HubMod",
            "Hub",
            hub.Id,
            admin.Id,
            expiresAt: DateTime.UtcNow.AddHours(4));

        // Act & Assert
        // Space access from permanent SpaceMod
        var spaceAccess = await _service.UserHasPermissionAsync(user.PublicId, "Moderate", "space", space.PublicId);
        await Assert.That(spaceAccess).IsTrue();

        // Hub access from temporary HubMod
        var hubAccess = await _service.UserHasPermissionAsync(user.PublicId, "Moderate", "hub", hub.PublicId);
        await Assert.That(hubAccess).IsTrue();
    }

    #endregion

    #region Explicit Permission Tests (2 tests)

    [Test]
    public async Task ExplicitPermission_Granted_ReturnsTrue()
    {
        // Arrange - user has a role with an explicit permission assigned
        var user = await _builder.CreateUserAsync();
        var admin = await _builder.CreateUserAsync("PermAdmin");

        // Create a role for the user
        var role = await _builder.AssignRoleAsync(user.Id, UserRoleTypeEnum.CommunityMod, admin.Id);

        // Create a permission and assign it to the role
        var permission = await _builder.CreatePermissionAsync("ManageReports", "Moderation");
        await _builder.AssignPermissionToRoleAsync(role.Id, permission.Id, admin.Id);

        // Act - check with the specific permission name, no scope (falls through to explicit check)
        var result = await _service.UserHasPermissionAsync(user.PublicId, "ManageReports");

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task ExplicitPermission_NotGranted_ReturnsFalse()
    {
        // Arrange - user has a role but the permission is not assigned to it
        var user = await _builder.CreateUserAsync();
        var admin = await _builder.CreateUserAsync("PermAdmin2");

        await _builder.AssignRoleAsync(user.Id, UserRoleTypeEnum.CommunityMod, admin.Id);

        // Create a permission but do NOT assign it to the user's role
        await _builder.CreatePermissionAsync("ManageReports", "Moderation");

        // Act
        var result = await _service.UserHasPermissionAsync(user.PublicId, "ManageReports");

        // Assert
        await Assert.That(result).IsFalse();
    }

    #endregion
}
