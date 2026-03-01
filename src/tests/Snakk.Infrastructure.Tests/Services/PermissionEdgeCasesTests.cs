using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.Services;
using Snakk.Shared.Enums;

namespace Snakk.Infrastructure.Tests.Services;

/// <summary>
/// Edge case tests for the PermissionService focusing on hierarchical access,
/// caching, and temporary role elevations.
/// </summary>
public class PermissionEdgeCasesTests : IDisposable
{
    private readonly SnakkDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly Mock<ILogger<PermissionService>> _mockLogger;
    private readonly Mock<ISecurityService> _mockSecurityService;
    private readonly PermissionService _service;

    public PermissionEdgeCasesTests()
    {
        var options = new DbContextOptionsBuilder<SnakkDbContext>()
            .UseInMemoryDatabase(databaseName: $"PermissionEdgeTests_{Guid.NewGuid()}")
            .Options;
        _context = new SnakkDbContext(options);
        _cache = new MemoryCache(new MemoryCacheOptions());
        _mockLogger = new Mock<ILogger<PermissionService>>();
        _mockSecurityService = new Mock<ISecurityService>();

        _service = new PermissionService(_context, _cache, _mockLogger.Object, _mockSecurityService.Object);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
        _cache.Dispose();
    }

    private async Task<UserDatabaseEntity> CreateUser(string publicId, string displayName = "Test User")
    {
        var user = new UserDatabaseEntity
        {
            PublicId = publicId,
            DisplayName = displayName,
            Email = $"{publicId}@example.com",
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    private async Task AssignRole(int userId, UserRoleTypeEnum roleType, int assignedByUserId,
        int? communityId = null, int? hubId = null, int? spaceId = null)
    {
        _context.UserRoles.Add(new UserRoleDatabaseEntity
        {
            PublicId = Ulid.NewUlid().ToString(),
            UserId = userId,
            RoleId = (int)roleType,
            CommunityId = communityId,
            HubId = hubId,
            SpaceId = spaceId,
            AssignedByUserId = assignedByUserId,
            AssignedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();
    }

    private async Task<CommunityDatabaseEntity> CreateCommunity(string publicId)
    {
        var community = new CommunityDatabaseEntity
        {
            PublicId = publicId,
            Name = $"Community {publicId}",
            Slug = publicId,
            CreatedAt = DateTime.UtcNow,
            VisibilityId = 1
        };
        _context.Communities.Add(community);
        await _context.SaveChangesAsync();
        return community;
    }

    private async Task<HubDatabaseEntity> CreateHub(int communityId, string publicId)
    {
        var hub = new HubDatabaseEntity
        {
            PublicId = publicId,
            CommunityId = communityId,
            Name = $"Hub {publicId}",
            Slug = publicId,
            CreatedAt = DateTime.UtcNow
        };
        _context.Hubs.Add(hub);
        await _context.SaveChangesAsync();
        return hub;
    }

    private async Task<SpaceDatabaseEntity> CreateSpace(int hubId, string publicId)
    {
        var space = new SpaceDatabaseEntity
        {
            PublicId = publicId,
            HubId = hubId,
            Name = $"Space {publicId}",
            Slug = publicId,
            CreatedAt = DateTime.UtcNow
        };
        _context.Spaces.Add(space);
        await _context.SaveChangesAsync();
        return space;
    }

    private async Task<DiscussionDatabaseEntity> CreateDiscussion(int spaceId, int userId, string publicId)
    {
        var discussion = new DiscussionDatabaseEntity
        {
            PublicId = publicId,
            SpaceId = spaceId,
            CreatedByUserId = userId,
            Title = $"Discussion {publicId}",
            Slug = publicId,
            CreatedAt = DateTime.UtcNow
        };
        _context.Discussions.Add(discussion);
        await _context.SaveChangesAsync();
        return discussion;
    }

    #region GlobalAdmin Has Access to Everything

    [Test]
    public async Task GlobalAdmin_HasAccessToAnyPermissionInAnyCommunity()
    {
        // Arrange
        var admin = await CreateUser("global-admin-1");
        var community = await CreateCommunity("ga-comm");
        await AssignRole(admin.Id, UserRoleTypeEnum.GlobalAdmin, admin.Id);

        // Act
        var result = await _service.UserHasPermissionAsync("global-admin-1", "AnyPermission", "community", community.PublicId);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task GlobalAdmin_HasAccessToAnyPermissionInAnyHub()
    {
        // Arrange
        var admin = await CreateUser("global-admin-hub");
        var community = await CreateCommunity("ga-hub-comm");
        var hub = await CreateHub(community.Id, "ga-hub");
        await AssignRole(admin.Id, UserRoleTypeEnum.GlobalAdmin, admin.Id);

        // Act
        var result = await _service.UserHasPermissionAsync("global-admin-hub", "AnyPermission", "hub", hub.PublicId);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task GlobalAdmin_HasAccessToAnyPermissionInAnySpace()
    {
        // Arrange
        var admin = await CreateUser("global-admin-space");
        var community = await CreateCommunity("ga-space-comm");
        var hub = await CreateHub(community.Id, "ga-space-hub");
        var space = await CreateSpace(hub.Id, "ga-space");
        await AssignRole(admin.Id, UserRoleTypeEnum.GlobalAdmin, admin.Id);

        // Act
        var result = await _service.UserHasPermissionAsync("global-admin-space", "AnyPermission", "space", space.PublicId);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task GlobalAdmin_HasAccessWithoutScope()
    {
        // Arrange
        var admin = await CreateUser("global-admin-noscope");
        await AssignRole(admin.Id, UserRoleTypeEnum.GlobalAdmin, admin.Id);

        // Act
        var result = await _service.UserHasPermissionAsync("global-admin-noscope", "AnyPermission");

        // Assert
        await Assert.That(result).IsTrue();
    }

    #endregion

    #region CommunityAdmin Has Access to Community and All Children

    [Test]
    public async Task CommunityAdmin_HasAccessToCommunity()
    {
        // Arrange
        var user = await CreateUser("comm-admin-edge");
        var community = await CreateCommunity("ca-edge-comm");
        await AssignRole(user.Id, UserRoleTypeEnum.CommunityAdmin, user.Id, communityId: community.Id);

        // Act
        var result = await _service.UserHasPermissionAsync("comm-admin-edge", "ManageCommunity", "community", community.PublicId);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task CommunityAdmin_HasAccessToChildHub()
    {
        // Arrange
        var user = await CreateUser("comm-admin-child-hub");
        var community = await CreateCommunity("ca-child-hub-comm");
        var hub = await CreateHub(community.Id, "ca-child-hub");
        await AssignRole(user.Id, UserRoleTypeEnum.CommunityAdmin, user.Id, communityId: community.Id);

        // Act
        var result = await _service.UserHasPermissionAsync("comm-admin-child-hub", "ManageHub", "hub", hub.PublicId);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task CommunityAdmin_HasAccessToGrandchildSpace()
    {
        // Arrange
        var user = await CreateUser("comm-admin-gc-space");
        var community = await CreateCommunity("ca-gc-comm");
        var hub = await CreateHub(community.Id, "ca-gc-hub");
        var space = await CreateSpace(hub.Id, "ca-gc-space");
        await AssignRole(user.Id, UserRoleTypeEnum.CommunityAdmin, user.Id, communityId: community.Id);

        // Act
        var result = await _service.UserHasPermissionAsync("comm-admin-gc-space", "ManageSpace", "space", space.PublicId);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task CommunityAdmin_HasAccessToDiscussionInCommunity()
    {
        // Arrange
        var user = await CreateUser("comm-admin-disc");
        var community = await CreateCommunity("ca-disc-comm");
        var hub = await CreateHub(community.Id, "ca-disc-hub");
        var space = await CreateSpace(hub.Id, "ca-disc-space");
        var discussion = await CreateDiscussion(space.Id, user.Id, "ca-disc-discussion");
        await AssignRole(user.Id, UserRoleTypeEnum.CommunityAdmin, user.Id, communityId: community.Id);

        // Act
        var result = await _service.UserHasPermissionAsync("comm-admin-disc", "ManageDiscussion", "discussion", discussion.PublicId);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task CommunityAdmin_NoAccessToOtherCommunity()
    {
        // Arrange
        var user = await CreateUser("comm-admin-other");
        var community1 = await CreateCommunity("ca-other-comm1");
        var community2 = await CreateCommunity("ca-other-comm2");
        await AssignRole(user.Id, UserRoleTypeEnum.CommunityAdmin, user.Id, communityId: community1.Id);

        // Act
        var result = await _service.UserHasPermissionAsync("comm-admin-other", "ManageCommunity", "community", community2.PublicId);

        // Assert
        await Assert.That(result).IsFalse();
    }

    #endregion

    #region HubMod Has Access to Hub and All Child Spaces

    [Test]
    public async Task HubMod_HasAccessToHub()
    {
        // Arrange
        var user = await CreateUser("hub-mod-edge");
        var community = await CreateCommunity("hm-edge-comm");
        var hub = await CreateHub(community.Id, "hm-edge-hub");
        await AssignRole(user.Id, UserRoleTypeEnum.HubMod, user.Id, hubId: hub.Id);

        // Act
        var result = await _service.UserHasPermissionAsync("hub-mod-edge", "ManageHub", "hub", hub.PublicId);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task HubMod_HasAccessToChildSpace()
    {
        // Arrange
        var user = await CreateUser("hub-mod-child-space");
        var community = await CreateCommunity("hm-child-comm");
        var hub = await CreateHub(community.Id, "hm-child-hub");
        var space = await CreateSpace(hub.Id, "hm-child-space");
        await AssignRole(user.Id, UserRoleTypeEnum.HubMod, user.Id, hubId: hub.Id);

        // Act
        var result = await _service.UserHasPermissionAsync("hub-mod-child-space", "ManageSpace", "space", space.PublicId);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task HubMod_NoAccessToOtherHub()
    {
        // Arrange
        var user = await CreateUser("hub-mod-other");
        var community = await CreateCommunity("hm-other-comm");
        var hub1 = await CreateHub(community.Id, "hm-other-hub1");
        var hub2 = await CreateHub(community.Id, "hm-other-hub2");
        await AssignRole(user.Id, UserRoleTypeEnum.HubMod, user.Id, hubId: hub1.Id);

        // Act
        var result = await _service.UserHasPermissionAsync("hub-mod-other", "ManageHub", "hub", hub2.PublicId);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task HubMod_NoAccessToSpaceInOtherHub()
    {
        // Arrange
        var user = await CreateUser("hub-mod-other-space");
        var community = await CreateCommunity("hm-os-comm");
        var hub1 = await CreateHub(community.Id, "hm-os-hub1");
        var hub2 = await CreateHub(community.Id, "hm-os-hub2");
        var space = await CreateSpace(hub2.Id, "hm-os-space");
        await AssignRole(user.Id, UserRoleTypeEnum.HubMod, user.Id, hubId: hub1.Id);

        // Act
        var result = await _service.UserHasPermissionAsync("hub-mod-other-space", "ManageSpace", "space", space.PublicId);

        // Assert
        await Assert.That(result).IsFalse();
    }

    #endregion

    #region SpaceMod Only Has Access to Their Space

    [Test]
    public async Task SpaceMod_HasAccessToOwnSpace()
    {
        // Arrange
        var user = await CreateUser("space-mod-own");
        var community = await CreateCommunity("sm-own-comm");
        var hub = await CreateHub(community.Id, "sm-own-hub");
        var space = await CreateSpace(hub.Id, "sm-own-space");
        await AssignRole(user.Id, UserRoleTypeEnum.SpaceMod, user.Id, spaceId: space.Id);

        // Act
        var result = await _service.UserHasPermissionAsync("space-mod-own", "ManageSpace", "space", space.PublicId);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task SpaceMod_NoAccessToOtherSpace()
    {
        // Arrange
        var user = await CreateUser("space-mod-other");
        var community = await CreateCommunity("sm-other-comm");
        var hub = await CreateHub(community.Id, "sm-other-hub");
        var space1 = await CreateSpace(hub.Id, "sm-other-space1");
        var space2 = await CreateSpace(hub.Id, "sm-other-space2");
        await AssignRole(user.Id, UserRoleTypeEnum.SpaceMod, user.Id, spaceId: space1.Id);

        // Act
        var result = await _service.UserHasPermissionAsync("space-mod-other", "ManageSpace", "space", space2.PublicId);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task SpaceMod_NoAccessToParentHub()
    {
        // Arrange
        var user = await CreateUser("space-mod-no-hub");
        var community = await CreateCommunity("sm-nh-comm");
        var hub = await CreateHub(community.Id, "sm-nh-hub");
        var space = await CreateSpace(hub.Id, "sm-nh-space");
        await AssignRole(user.Id, UserRoleTypeEnum.SpaceMod, user.Id, spaceId: space.Id);

        // Act
        var result = await _service.UserHasPermissionAsync("space-mod-no-hub", "ManageHub", "hub", hub.PublicId);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task SpaceMod_HasAccessToDiscussionInOwnSpace()
    {
        // Arrange
        var user = await CreateUser("space-mod-disc-own");
        var community = await CreateCommunity("sm-do-comm");
        var hub = await CreateHub(community.Id, "sm-do-hub");
        var space = await CreateSpace(hub.Id, "sm-do-space");
        var discussion = await CreateDiscussion(space.Id, user.Id, "sm-do-disc");
        await AssignRole(user.Id, UserRoleTypeEnum.SpaceMod, user.Id, spaceId: space.Id);

        // Act
        var result = await _service.UserHasPermissionAsync("space-mod-disc-own", "ManageDiscussion", "discussion", discussion.PublicId);

        // Assert
        await Assert.That(result).IsTrue();
    }

    #endregion

    #region Permission Caching (5-Minute TTL)

    [Test]
    public async Task PermissionCaching_SameQueryTwice_UsesCachedResult()
    {
        // Arrange
        var user = await CreateUser("cache-user");
        var role = new UserRoleDatabaseEntity
        {
            PublicId = Ulid.NewUlid().ToString(),
            UserId = user.Id,
            RoleId = (int)UserRoleTypeEnum.CommunityMod,
            AssignedByUserId = user.Id,
            AssignedAt = DateTime.UtcNow
        };
        _context.UserRoles.Add(role);
        await _context.SaveChangesAsync();

        var permission = new PermissionDatabaseEntity
        {
            PublicId = "cached-perm",
            Name = "CachedPermission",
            Category = "Test",
            CreatedAt = DateTime.UtcNow
        };
        _context.Permissions.Add(permission);
        await _context.SaveChangesAsync();

        _context.RolePermissions.Add(new RolePermissionDatabaseEntity
        {
            RoleId = role.Id,
            PermissionId = permission.Id,
            GrantedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        // Act - first call should populate cache
        var result1 = await _service.GetUserPermissionsAsync("cache-user");
        await Assert.That(result1.Count).IsEqualTo(1);

        // Second call should use cache
        var result2 = await _service.GetUserPermissionsAsync("cache-user");
        await Assert.That(result2.Count).IsEqualTo(1);
    }

    #endregion

    #region Temporary Role Elevation

    [Test]
    public async Task TemporaryRoleElevation_GrantsAccess()
    {
        // Arrange
        var user = await CreateUser("temp-elev-user");
        var admin = await CreateUser("temp-elev-admin");
        var community = await CreateCommunity("temp-elev-comm");
        var hub = await CreateHub(community.Id, "temp-elev-hub");
        var expiresAt = DateTime.UtcNow.AddHours(4);

        // Act - grant temporary HubMod role
        var result = await _service.GrantTemporaryRoleAsync(
            "temp-elev-user", "HubMod", "Hub", hub.Id, expiresAt, "Emergency coverage", "temp-elev-admin");

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.RoleType).IsEqualTo("HubMod");
        await Assert.That(result.Scope).IsEqualTo("Hub");
        await Assert.That(result.ExpiresAt).IsEqualTo(expiresAt);
        await Assert.That(result.Reason).IsEqualTo("Emergency coverage");

        // Verify the elevation was stored
        var elevation = await _context.TemporaryRoleElevations.FirstAsync();
        await Assert.That(elevation.RevokedAt).IsNull();
    }

    [Test]
    public async Task TemporaryRoleElevation_Revocation_SetsRevokedFields()
    {
        // Arrange
        var user = await CreateUser("temp-revoke-user");
        var admin = await CreateUser("temp-revoke-admin");

        var elevation = new TemporaryRoleElevationDatabaseEntity
        {
            PublicId = "elev-to-revoke-edge",
            UserId = user.Id,
            RoleType = "HubMod",
            Scope = "Hub",
            ScopeId = 1,
            ExpiresAt = DateTime.UtcNow.AddHours(4),
            GrantedById = admin.Id,
            CreatedAt = DateTime.UtcNow
        };
        _context.TemporaryRoleElevations.Add(elevation);
        await _context.SaveChangesAsync();

        // Act
        await _service.RevokeTemporaryRoleAsync("elev-to-revoke-edge", "No longer needed", "temp-revoke-admin");

        // Assert
        var revoked = await _context.TemporaryRoleElevations.FirstAsync(e => e.PublicId == "elev-to-revoke-edge");
        await Assert.That(revoked.RevokedAt).IsNotNull();
        await Assert.That(revoked.RevokedReason).IsEqualTo("No longer needed");
        await Assert.That(revoked.RevokedById).IsEqualTo(admin.Id);
    }

    [Test]
    public async Task TemporaryRoleElevation_NonExistent_DoesNotThrow()
    {
        // Act & Assert
        var act = async () => await _service.RevokeTemporaryRoleAsync("nonexistent-elevation", "reason", "admin");
        await Assert.That(act).ThrowsNothing();
    }

    [Test]
    public async Task TemporaryRoleElevation_InvalidUser_Throws()
    {
        // Arrange
        var admin = await CreateUser("temp-inv-admin");

        // Act & Assert
        var act = async () => await _service.GrantTemporaryRoleAsync(
            "nonexistent-user", "HubMod", "Hub", 1, DateTime.UtcNow.AddHours(1), "reason", "temp-inv-admin");
        await Assert.That(act).ThrowsException();
    }

    [Test]
    public async Task TemporaryRoleElevation_InvalidAdmin_Throws()
    {
        // Arrange
        var user = await CreateUser("temp-inv-user");

        // Act & Assert
        var act = async () => await _service.GrantTemporaryRoleAsync(
            "temp-inv-user", "HubMod", "Hub", 1, DateTime.UtcNow.AddHours(1), "reason", "nonexistent-admin");
        await Assert.That(act).ThrowsException();
    }

    #endregion

    #region Revoked Role Not Considered

    [Test]
    public async Task RevokedRole_NotConsidered_InHierarchicalAccess()
    {
        // Arrange
        var user = await CreateUser("revoked-hier-user");
        var community = await CreateCommunity("revoked-hier-comm");

        _context.UserRoles.Add(new UserRoleDatabaseEntity
        {
            PublicId = Ulid.NewUlid().ToString(),
            UserId = user.Id,
            RoleId = (int)UserRoleTypeEnum.CommunityAdmin,
            CommunityId = community.Id,
            AssignedByUserId = user.Id,
            AssignedAt = DateTime.UtcNow,
            RevokedAt = DateTime.UtcNow // Already revoked
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.UserHasPermissionAsync("revoked-hier-user", "ManageCommunity", "community", community.PublicId);

        // Assert
        await Assert.That(result).IsFalse();
    }

    #endregion

    #region Non-Existent Entity IDs

    [Test]
    public async Task NonExistentHub_ReturnsFalse()
    {
        // Arrange
        var user = await CreateUser("nonexist-hub-user");
        await AssignRole(user.Id, UserRoleTypeEnum.HubMod, user.Id, hubId: 99999);

        // Act
        var result = await _service.UserHasPermissionAsync("nonexist-hub-user", "ManageHub", "hub", "88888");

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task NonExistentSpace_ReturnsFalse()
    {
        // Arrange
        var user = await CreateUser("nonexist-space-user");
        await AssignRole(user.Id, UserRoleTypeEnum.SpaceMod, user.Id, spaceId: 99999);

        // Act
        var result = await _service.UserHasPermissionAsync("nonexist-space-user", "ManageSpace", "space", "88888");

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task NonExistentDiscussion_ReturnsFalse()
    {
        // Arrange
        var user = await CreateUser("nonexist-disc-user");

        // Act
        var result = await _service.UserHasPermissionAsync("nonexist-disc-user", "ManageDiscussion", "discussion", "99999");

        // Assert
        await Assert.That(result).IsFalse();
    }

    #endregion

    #region Unknown Scope

    [Test]
    public async Task UnknownScope_ReturnsFalse()
    {
        // Arrange
        var user = await CreateUser("unknown-scope-edge");
        var community = await CreateCommunity("us-edge-comm");
        await AssignRole(user.Id, UserRoleTypeEnum.CommunityAdmin, user.Id, communityId: community.Id);

        // Act
        var result = await _service.UserHasPermissionAsync("unknown-scope-edge", "SomePerm", "nonexistentscope", "1");

        // Assert
        await Assert.That(result).IsFalse();
    }

    #endregion
}
