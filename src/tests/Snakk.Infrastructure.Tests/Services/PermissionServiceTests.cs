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

public class PermissionServiceTests : IDisposable
{
    private readonly SnakkDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly Mock<ILogger<PermissionService>> _mockLogger;
    private readonly Mock<ISecurityService> _mockSecurityService;
    private readonly PermissionService _service;

    public PermissionServiceTests()
    {
        var options = new DbContextOptionsBuilder<SnakkDbContext>()
            .UseInMemoryDatabase(databaseName: $"PermissionTests_{Guid.NewGuid()}")
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

    private async Task<UserDatabaseEntity> CreateUser(string publicId = "user1", string displayName = "Test User")
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

    private async Task<CommunityDatabaseEntity> CreateCommunity(string publicId = "comm1")
    {
        var community = new CommunityDatabaseEntity
        {
            PublicId = publicId,
            Name = "Test Community",
            Slug = publicId,
            CreatedAt = DateTime.UtcNow,
            VisibilityId = 1
        };
        _context.Communities.Add(community);
        await _context.SaveChangesAsync();
        return community;
    }

    private async Task<HubDatabaseEntity> CreateHub(int communityId, string publicId = "hub1")
    {
        var hub = new HubDatabaseEntity
        {
            PublicId = publicId,
            CommunityId = communityId,
            Name = "Test Hub",
            Slug = publicId,
            CreatedAt = DateTime.UtcNow
        };
        _context.Hubs.Add(hub);
        await _context.SaveChangesAsync();
        return hub;
    }

    private async Task<SpaceDatabaseEntity> CreateSpace(int hubId, string publicId = "space1")
    {
        var space = new SpaceDatabaseEntity
        {
            PublicId = publicId,
            HubId = hubId,
            Name = "Test Space",
            Slug = publicId,
            CreatedAt = DateTime.UtcNow
        };
        _context.Spaces.Add(space);
        await _context.SaveChangesAsync();
        return space;
    }

    private async Task<DiscussionDatabaseEntity> CreateDiscussion(int spaceId, int userId, string publicId = "disc1")
    {
        var discussion = new DiscussionDatabaseEntity
        {
            PublicId = publicId,
            SpaceId = spaceId,
            CreatedByUserId = userId,
            Title = "Test Discussion",
            Slug = publicId,
            CreatedAt = DateTime.UtcNow
        };
        _context.Discussions.Add(discussion);
        await _context.SaveChangesAsync();
        return discussion;
    }

    #region GlobalAdmin Tests

    [Test]
    public async Task UserHasPermissionAsync_GlobalAdmin_HasAccessToEverything()
    {
        var admin = await CreateUser("admin");
        await AssignRole(admin.Id, UserRoleTypeEnum.GlobalAdmin, admin.Id);

        var result = await _service.UserHasPermissionAsync("admin", "AnyPermission", "community", "1");

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task UserHasPermissionAsync_GlobalAdmin_HasAccessWithoutScope()
    {
        var admin = await CreateUser("admin_noscope");
        await AssignRole(admin.Id, UserRoleTypeEnum.GlobalAdmin, admin.Id);

        var result = await _service.UserHasPermissionAsync("admin_noscope", "AnyPermission");

        await Assert.That(result).IsTrue();
    }

    #endregion

    #region NonExistent User Tests

    [Test]
    public async Task UserHasPermissionAsync_NonexistentUser_ReturnsFalse()
    {
        var result = await _service.UserHasPermissionAsync("nonexistent", "SomePermission");

        await Assert.That(result).IsFalse();
    }

    #endregion

    #region Community Scope Tests

    [Test]
    public async Task UserHasPermissionAsync_CommunityAdmin_HasAccessToCommunity()
    {
        var user = await CreateUser("comm_admin");
        var community = await CreateCommunity("test_comm");
        await AssignRole(user.Id, UserRoleTypeEnum.CommunityAdmin, user.Id, communityId: community.Id);

        var result = await _service.UserHasPermissionAsync("comm_admin", "ManageCommunity", "community", community.PublicId);

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task UserHasPermissionAsync_CommunityAdmin_NoAccessToOtherCommunity()
    {
        var user = await CreateUser("comm_admin2");
        var community1 = await CreateCommunity("comm1_scope");
        var community2 = await CreateCommunity("comm2_scope");
        await AssignRole(user.Id, UserRoleTypeEnum.CommunityAdmin, user.Id, communityId: community1.Id);

        var result = await _service.UserHasPermissionAsync("comm_admin2", "ManageCommunity", "community", community2.PublicId);

        await Assert.That(result).IsFalse();
    }

    #endregion

    #region Hub Scope Tests

    [Test]
    public async Task UserHasPermissionAsync_HubMod_HasAccessToHub()
    {
        var user = await CreateUser("hub_mod");
        var community = await CreateCommunity("hub_comm");
        var hub = await CreateHub(community.Id, "test_hub");
        await AssignRole(user.Id, UserRoleTypeEnum.HubMod, user.Id, hubId: hub.Id);

        var result = await _service.UserHasPermissionAsync("hub_mod", "ManageHub", "hub", hub.PublicId);

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task UserHasPermissionAsync_CommunityAdmin_HasAccessToChildHub()
    {
        var user = await CreateUser("comm_admin_hub");
        var community = await CreateCommunity("comm_hub_scope");
        var hub = await CreateHub(community.Id, "child_hub");
        await AssignRole(user.Id, UserRoleTypeEnum.CommunityAdmin, user.Id, communityId: community.Id);

        var result = await _service.UserHasPermissionAsync("comm_admin_hub", "ManageHub", "hub", hub.PublicId);

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task UserHasPermissionAsync_HubMod_NoAccessToOtherHub()
    {
        var user = await CreateUser("hub_mod2");
        var community = await CreateCommunity("hub2_comm");
        var hub1 = await CreateHub(community.Id, "hub_a");
        var hub2 = await CreateHub(community.Id, "hub_b");
        await AssignRole(user.Id, UserRoleTypeEnum.HubMod, user.Id, hubId: hub1.Id);

        var result = await _service.UserHasPermissionAsync("hub_mod2", "ManageHub", "hub", hub2.PublicId);

        await Assert.That(result).IsFalse();
    }

    #endregion

    #region Space Scope Tests

    [Test]
    public async Task UserHasPermissionAsync_SpaceMod_HasAccessToSpace()
    {
        var user = await CreateUser("space_mod");
        var community = await CreateCommunity("space_comm");
        var hub = await CreateHub(community.Id, "space_hub");
        var space = await CreateSpace(hub.Id, "test_space");
        await AssignRole(user.Id, UserRoleTypeEnum.SpaceMod, user.Id, spaceId: space.Id);

        var result = await _service.UserHasPermissionAsync("space_mod", "ManageSpace", "space", space.PublicId);

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task UserHasPermissionAsync_HubMod_HasAccessToChildSpace()
    {
        var user = await CreateUser("hub_mod_space");
        var community = await CreateCommunity("hms_comm");
        var hub = await CreateHub(community.Id, "hms_hub");
        var space = await CreateSpace(hub.Id, "hms_space");
        await AssignRole(user.Id, UserRoleTypeEnum.HubMod, user.Id, hubId: hub.Id);

        var result = await _service.UserHasPermissionAsync("hub_mod_space", "ManageSpace", "space", space.PublicId);

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task UserHasPermissionAsync_CommunityAdmin_HasAccessToChildSpace()
    {
        var user = await CreateUser("comm_admin_space");
        var community = await CreateCommunity("cas_comm");
        var hub = await CreateHub(community.Id, "cas_hub");
        var space = await CreateSpace(hub.Id, "cas_space");
        await AssignRole(user.Id, UserRoleTypeEnum.CommunityAdmin, user.Id, communityId: community.Id);

        var result = await _service.UserHasPermissionAsync("comm_admin_space", "ManageSpace", "space", space.PublicId);

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task UserHasPermissionAsync_SpaceMod_NoAccessToOtherSpace()
    {
        var user = await CreateUser("space_mod2");
        var community = await CreateCommunity("sm2_comm");
        var hub = await CreateHub(community.Id, "sm2_hub");
        var space1 = await CreateSpace(hub.Id, "space_a");
        var space2 = await CreateSpace(hub.Id, "space_b");
        await AssignRole(user.Id, UserRoleTypeEnum.SpaceMod, user.Id, spaceId: space1.Id);

        var result = await _service.UserHasPermissionAsync("space_mod2", "ManageSpace", "space", space2.PublicId);

        await Assert.That(result).IsFalse();
    }

    #endregion

    #region Discussion Scope Tests

    [Test]
    public async Task UserHasPermissionAsync_SpaceMod_HasAccessToDiscussionInSpace()
    {
        var user = await CreateUser("space_mod_disc");
        var community = await CreateCommunity("smd_comm");
        var hub = await CreateHub(community.Id, "smd_hub");
        var space = await CreateSpace(hub.Id, "smd_space");
        var discussion = await CreateDiscussion(space.Id, user.Id, "smd_disc");
        await AssignRole(user.Id, UserRoleTypeEnum.SpaceMod, user.Id, spaceId: space.Id);

        var result = await _service.UserHasPermissionAsync("space_mod_disc", "ManageDiscussion", "discussion", discussion.PublicId);

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task UserHasPermissionAsync_Discussion_NonexistentId_ReturnsFalse()
    {
        var user = await CreateUser("disc_nonexist");
        await AssignRole(user.Id, UserRoleTypeEnum.GlobalAdmin, user.Id);

        // GlobalAdmin check happens before discussion lookup, so GlobalAdmin still passes.
        // Test with a non-admin user instead.
        var normalUser = await CreateUser("normal_disc");
        var result = await _service.UserHasPermissionAsync("normal_disc", "ManageDiscussion", "discussion", "99999");

        await Assert.That(result).IsFalse();
    }

    #endregion

    #region Temporary Role Elevation Tests

    [Test]
    public async Task GrantTemporaryRoleAsync_CreatesElevation()
    {
        var user = await CreateUser("temp_user");
        var admin = await CreateUser("temp_admin");
        var expiresAt = DateTime.UtcNow.AddHours(4);

        var result = await _service.GrantTemporaryRoleAsync(
            "temp_user", "HubMod", "Hub", 1, expiresAt, "Emergency coverage", "temp_admin");

        await Assert.That(result).IsNotNull();
        await Assert.That(result.RoleType).IsEqualTo("HubMod");
        await Assert.That(result.Scope).IsEqualTo("Hub");
        await Assert.That(result.Reason).IsEqualTo("Emergency coverage");

        var elevation = await _context.TemporaryRoleElevations.FirstAsync();
        await Assert.That(elevation).IsNotNull();
    }

    [Test]
    public async Task GrantTemporaryRoleAsync_InvalidUser_Throws()
    {
        var admin = await CreateUser("grant_admin");

        var act = async () => await _service.GrantTemporaryRoleAsync(
            "nonexistent", "HubMod", "Hub", 1, DateTime.UtcNow.AddHours(1), "reason", "grant_admin");

        await Assert.That(act).ThrowsException();
    }

    [Test]
    public async Task GrantTemporaryRoleAsync_InvalidAdmin_Throws()
    {
        var user = await CreateUser("grant_user");

        var act = async () => await _service.GrantTemporaryRoleAsync(
            "grant_user", "HubMod", "Hub", 1, DateTime.UtcNow.AddHours(1), "reason", "nonexistent_admin");

        await Assert.That(act).ThrowsException();
    }

    [Test]
    public async Task RevokeTemporaryRoleAsync_RevokesElevation()
    {
        var user = await CreateUser("revoke_temp_user");
        var admin = await CreateUser("revoke_temp_admin");

        var elevation = new TemporaryRoleElevationDatabaseEntity
        {
            PublicId = "elev_to_revoke",
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

        await _service.RevokeTemporaryRoleAsync("elev_to_revoke", "No longer needed", "revoke_temp_admin");

        var revoked = await _context.TemporaryRoleElevations.FirstAsync(e => e.PublicId == "elev_to_revoke");
        await Assert.That(revoked.RevokedAt).IsNotNull();
        await Assert.That(revoked.RevokedReason).IsEqualTo("No longer needed");
    }

    [Test]
    public async Task RevokeTemporaryRoleAsync_AlreadyRevoked_DoesNothing()
    {
        // Should complete without error
        var act = async () => await _service.RevokeTemporaryRoleAsync("nonexistent_elev", "reason", "admin");
        await Assert.That(act).ThrowsNothing();
    }

    #endregion

    #region GetUserPermissionsAsync Tests

    [Test]
    public async Task GetUserPermissionsAsync_ReturnsPermissions()
    {
        var user = await CreateUser("perm_user");
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
            PublicId = "perm1",
            Name = "ManageUsers",
            Category = "Admin",
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

        var result = await _service.GetUserPermissionsAsync("perm_user");

        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result.First().Name).IsEqualTo("ManageUsers");
    }

    [Test]
    public async Task GetUserPermissionsAsync_NonexistentUser_ReturnsEmpty()
    {
        var result = await _service.GetUserPermissionsAsync("nonexistent");

        await Assert.That(result.Count).IsEqualTo(0);
    }

    #endregion

    #region GetAllPermissionsAsync Tests

    [Test]
    public async Task GetAllPermissionsAsync_ReturnsAllPermissions()
    {
        _context.Permissions.AddRange(
            new PermissionDatabaseEntity
            {
                PublicId = "p1",
                Name = "ManageUsers",
                Category = "Admin",
                CreatedAt = DateTime.UtcNow
            },
            new PermissionDatabaseEntity
            {
                PublicId = "p2",
                Name = "ManageContent",
                Category = "Content",
                CreatedAt = DateTime.UtcNow
            }
        );
        await _context.SaveChangesAsync();

        var result = await _service.GetAllPermissionsAsync();

        await Assert.That(result.Count).IsEqualTo(2);
    }

    #endregion

    #region Revoked Role Tests

    [Test]
    public async Task UserHasPermissionAsync_RevokedRole_NotConsidered()
    {
        var user = await CreateUser("revoked_role_user");
        var community = await CreateCommunity("revoked_comm");

        _context.UserRoles.Add(new UserRoleDatabaseEntity
        {
            PublicId = Ulid.NewUlid().ToString(),
            UserId = user.Id,
            RoleId = (int)UserRoleTypeEnum.CommunityAdmin,
            CommunityId = community.Id,
            AssignedByUserId = user.Id,
            AssignedAt = DateTime.UtcNow,
            RevokedAt = DateTime.UtcNow // Role is revoked
        });
        await _context.SaveChangesAsync();

        var result = await _service.UserHasPermissionAsync("revoked_role_user", "ManageCommunity", "community", community.PublicId);

        await Assert.That(result).IsFalse();
    }

    #endregion

    #region Unknown Scope Tests

    [Test]
    public async Task UserHasPermissionAsync_UnknownScope_ReturnsFalse()
    {
        var user = await CreateUser("unknown_scope_user");
        await AssignRole(user.Id, UserRoleTypeEnum.CommunityMod, user.Id, communityId: 1);

        var result = await _service.UserHasPermissionAsync("unknown_scope_user", "SomePerm", "unknownscope", "1");

        await Assert.That(result).IsFalse();
    }

    #endregion
}
