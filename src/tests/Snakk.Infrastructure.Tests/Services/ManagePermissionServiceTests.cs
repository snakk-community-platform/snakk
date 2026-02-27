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

public class ManagePermissionServiceTests : IDisposable
{
    private readonly SnakkDbContext _context;
    private readonly ManagePermissionService _service;
    private readonly IMemoryCache _cache;

    public ManagePermissionServiceTests()
    {
        var options = new DbContextOptionsBuilder<SnakkDbContext>()
            .UseInMemoryDatabase(databaseName: $"ManagePermissionServiceTests_{Guid.NewGuid()}")
            .Options;
        _context = new SnakkDbContext(options);
        _cache = new MemoryCache(new MemoryCacheOptions());
        _service = new ManagePermissionService(_context, _cache, new Mock<ILogger<ManagePermissionService>>().Object);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
        _cache.Dispose();
    }

    #region Helpers

    private async Task<UserDatabaseEntity> CreateUser(string publicId = "user-001", string displayName = "Test User")
    {
        var user = new UserDatabaseEntity
        {
            PublicId = publicId,
            DisplayName = displayName,
            Email = $"{publicId}@test.com",
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    private async Task<CommunityDatabaseEntity> CreateCommunity(string publicId = "comm-001")
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

    private async Task<HubDatabaseEntity> CreateHub(int communityId, string publicId = "hub-001")
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

    private async Task<SpaceDatabaseEntity> CreateSpace(int hubId, string publicId = "space-001")
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

    private async Task AssignRole(int userId, UserRoleTypeEnum roleType, int assignedByUserId,
        int? communityId = null, int? hubId = null, int? spaceId = null, string? publicId = null)
    {
        _context.UserRoles.Add(new UserRoleDatabaseEntity
        {
            PublicId = publicId ?? Ulid.NewUlid().ToString(),
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

    /// <summary>
    /// Creates the full hierarchy: Community -> Hub -> Space and returns all three.
    /// </summary>
    private async Task<(CommunityDatabaseEntity Community, HubDatabaseEntity Hub, SpaceDatabaseEntity Space)> CreateHierarchy(
        string commId = "comm-001", string hubId = "hub-001", string spaceId = "space-001")
    {
        var community = await CreateCommunity(commId);
        var hub = await CreateHub(community.Id, hubId);
        var space = await CreateSpace(hub.Id, spaceId);
        return (community, hub, space);
    }

    #endregion

    #region Global Admin Tests

    [Test]
    public async Task GlobalAdmin_HasAllPermissions()
    {
        var user = await CreateUser("global-admin");
        await AssignRole(user.Id, UserRoleTypeEnum.GlobalAdmin, user.Id);
        var (community, _, _) = await CreateHierarchy();

        var result = await _service.GetPermissionsForScopeAsync("global-admin", "community", community.Id);

        await Assert.That(result.ViewDashboard).IsTrue();
        await Assert.That(result.ManageContent).IsTrue();
        await Assert.That(result.ManageReports).IsTrue();
        await Assert.That(result.ManageBans).IsTrue();
        await Assert.That(result.ManageSettings).IsTrue();
        await Assert.That(result.ManageTeam).IsTrue();
        await Assert.That(result.ManageWebhooks).IsTrue();
    }

    [Test]
    public async Task GlobalAdmin_HasPermission_ManageSettings()
    {
        var user = await CreateUser("global-admin-settings");
        await AssignRole(user.Id, UserRoleTypeEnum.GlobalAdmin, user.Id);
        var (community, _, _) = await CreateHierarchy("comm-ga-settings", "hub-ga-settings", "space-ga-settings");

        var result = await _service.HasPermissionAsync("global-admin-settings", "community", community.Id, ManagePermissionEnum.ManageSettings);

        await Assert.That(result).IsTrue();
    }

    #endregion

    #region Community Scope Tests

    [Test]
    public async Task CommunityAdmin_AtCommunityScope_HasAllPermissions()
    {
        var user = await CreateUser("comm-admin");
        var (community, _, _) = await CreateHierarchy("comm-ca", "hub-ca", "space-ca");
        await AssignRole(user.Id, UserRoleTypeEnum.CommunityAdmin, user.Id, communityId: community.Id);

        var result = await _service.GetPermissionsForScopeAsync("comm-admin", "community", community.Id);

        await Assert.That(result.ViewDashboard).IsTrue();
        await Assert.That(result.ManageContent).IsTrue();
        await Assert.That(result.ManageReports).IsTrue();
        await Assert.That(result.ManageBans).IsTrue();
        await Assert.That(result.ManageSettings).IsTrue();
        await Assert.That(result.ManageTeam).IsTrue();
        await Assert.That(result.ManageWebhooks).IsTrue();
    }

    [Test]
    public async Task CommunityMod_AtCommunityScope_HasModeratorPermissions()
    {
        var user = await CreateUser("comm-mod");
        var (community, _, _) = await CreateHierarchy("comm-cm", "hub-cm", "space-cm");
        await AssignRole(user.Id, UserRoleTypeEnum.CommunityMod, user.Id, communityId: community.Id);

        var result = await _service.GetPermissionsForScopeAsync("comm-mod", "community", community.Id);

        // Moderator permissions are granted
        await Assert.That(result.ViewDashboard).IsTrue();
        await Assert.That(result.ManageContent).IsTrue();
        await Assert.That(result.ManageReports).IsTrue();
        await Assert.That(result.ManageBans).IsTrue();

        // Admin-only permissions are NOT granted
        await Assert.That(result.ManageSettings).IsFalse();
        await Assert.That(result.ManageTeam).IsFalse();
        await Assert.That(result.ManageWebhooks).IsFalse();
    }

    #endregion

    #region Hub Scope Tests (Bubbles Down)

    [Test]
    public async Task CommunityAdmin_AtHubScope_HasAllPermissions()
    {
        var user = await CreateUser("comm-admin-hub");
        var (community, hub, _) = await CreateHierarchy("comm-cah", "hub-cah", "space-cah");
        await AssignRole(user.Id, UserRoleTypeEnum.CommunityAdmin, user.Id, communityId: community.Id);

        var result = await _service.GetPermissionsForScopeAsync("comm-admin-hub", "hub", hub.Id);

        await Assert.That(result.ViewDashboard).IsTrue();
        await Assert.That(result.ManageContent).IsTrue();
        await Assert.That(result.ManageReports).IsTrue();
        await Assert.That(result.ManageBans).IsTrue();
        await Assert.That(result.ManageSettings).IsTrue();
        await Assert.That(result.ManageTeam).IsTrue();
        await Assert.That(result.ManageWebhooks).IsTrue();
    }

    [Test]
    public async Task HubMod_AtHubScope_HasModeratorPermissions()
    {
        var user = await CreateUser("hub-mod");
        var (community, hub, _) = await CreateHierarchy("comm-hm", "hub-hm", "space-hm");
        await AssignRole(user.Id, UserRoleTypeEnum.HubMod, user.Id, hubId: hub.Id);

        var result = await _service.GetPermissionsForScopeAsync("hub-mod", "hub", hub.Id);

        // Moderator permissions are granted
        await Assert.That(result.ViewDashboard).IsTrue();
        await Assert.That(result.ManageContent).IsTrue();
        await Assert.That(result.ManageReports).IsTrue();
        await Assert.That(result.ManageBans).IsTrue();

        // Admin-only permissions are NOT granted
        await Assert.That(result.ManageSettings).IsFalse();
        await Assert.That(result.ManageTeam).IsFalse();
        await Assert.That(result.ManageWebhooks).IsFalse();
    }

    #endregion

    #region Space Scope Tests (Bubbles Down 2 Levels)

    [Test]
    public async Task CommunityAdmin_AtSpaceScope_HasAllPermissions()
    {
        var user = await CreateUser("comm-admin-space");
        var (community, _, space) = await CreateHierarchy("comm-cas", "hub-cas", "space-cas");
        await AssignRole(user.Id, UserRoleTypeEnum.CommunityAdmin, user.Id, communityId: community.Id);

        var result = await _service.GetPermissionsForScopeAsync("comm-admin-space", "space", space.Id);

        await Assert.That(result.ViewDashboard).IsTrue();
        await Assert.That(result.ManageContent).IsTrue();
        await Assert.That(result.ManageReports).IsTrue();
        await Assert.That(result.ManageBans).IsTrue();
        await Assert.That(result.ManageSettings).IsTrue();
        await Assert.That(result.ManageTeam).IsTrue();
        await Assert.That(result.ManageWebhooks).IsTrue();
    }

    [Test]
    public async Task SpaceMod_AtSpaceScope_HasModeratorPermissions()
    {
        var user = await CreateUser("space-mod");
        var (_, _, space) = await CreateHierarchy("comm-sm", "hub-sm", "space-sm");
        await AssignRole(user.Id, UserRoleTypeEnum.SpaceMod, user.Id, spaceId: space.Id);

        var result = await _service.GetPermissionsForScopeAsync("space-mod", "space", space.Id);

        // Moderator permissions are granted
        await Assert.That(result.ViewDashboard).IsTrue();
        await Assert.That(result.ManageContent).IsTrue();
        await Assert.That(result.ManageReports).IsTrue();
        await Assert.That(result.ManageBans).IsTrue();

        // Admin-only permissions are NOT granted
        await Assert.That(result.ManageSettings).IsFalse();
        await Assert.That(result.ManageTeam).IsFalse();
        await Assert.That(result.ManageWebhooks).IsFalse();
    }

    #endregion

    #region Cross-Scope Isolation Tests

    [Test]
    public async Task CommunityAdmin_OfDifferentCommunity_HasNoPermissions()
    {
        var user = await CreateUser("cross-admin");
        var (communityA, _, _) = await CreateHierarchy("comm-a", "hub-a", "space-a");
        var communityB = await CreateCommunity("comm-b");

        // User is admin of community A
        await AssignRole(user.Id, UserRoleTypeEnum.CommunityAdmin, user.Id, communityId: communityA.Id);

        // Check permissions for community B (should have none)
        var result = await _service.GetPermissionsForScopeAsync("cross-admin", "community", communityB.Id);

        await Assert.That(result.ViewDashboard).IsFalse();
        await Assert.That(result.ManageContent).IsFalse();
        await Assert.That(result.ManageReports).IsFalse();
        await Assert.That(result.ManageBans).IsFalse();
        await Assert.That(result.ManageSettings).IsFalse();
        await Assert.That(result.ManageTeam).IsFalse();
        await Assert.That(result.ManageWebhooks).IsFalse();
        await Assert.That(result.HasAnyPermission).IsFalse();
    }

    #endregion

    #region Edge Case Tests

    [Test]
    public async Task NonexistentUser_HasNoPermissions()
    {
        var (community, _, _) = await CreateHierarchy("comm-ne", "hub-ne", "space-ne");

        var result = await _service.GetPermissionsForScopeAsync("nonexistent-user", "community", community.Id);

        await Assert.That(result.ViewDashboard).IsFalse();
        await Assert.That(result.ManageContent).IsFalse();
        await Assert.That(result.ManageReports).IsFalse();
        await Assert.That(result.ManageBans).IsFalse();
        await Assert.That(result.ManageSettings).IsFalse();
        await Assert.That(result.ManageTeam).IsFalse();
        await Assert.That(result.ManageWebhooks).IsFalse();
        await Assert.That(result.HasAnyPermission).IsFalse();
    }

    [Test]
    public async Task TemporaryElevation_GrantsPermissions()
    {
        var user = await CreateUser("temp-elevated");
        var admin = await CreateUser("temp-admin");
        var (community, _, _) = await CreateHierarchy("comm-te", "hub-te", "space-te");

        // User has no permanent roles, but has a temporary CommunityMod elevation
        _context.TemporaryRoleElevations.Add(new TemporaryRoleElevationDatabaseEntity
        {
            PublicId = "temp-001",
            UserId = user.Id,
            RoleType = "CommunityMod",
            Scope = "Community",
            ScopeId = community.Id,
            ExpiresAt = DateTime.UtcNow.AddHours(4),
            Reason = "Testing temporary elevation",
            GrantedById = admin.Id,
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var result = await _service.GetPermissionsForScopeAsync("temp-elevated", "community", community.Id);

        // Should get moderator permissions from the temporary elevation
        await Assert.That(result.ViewDashboard).IsTrue();
        await Assert.That(result.ManageContent).IsTrue();
        await Assert.That(result.ManageReports).IsTrue();
        await Assert.That(result.ManageBans).IsTrue();

        // Admin-only permissions remain denied
        await Assert.That(result.ManageSettings).IsFalse();
        await Assert.That(result.ManageTeam).IsFalse();
        await Assert.That(result.ManageWebhooks).IsFalse();
    }

    #endregion
}
