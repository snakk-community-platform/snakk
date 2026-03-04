using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Snakk.Api.Tests.Helpers;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Shared.Enums;

namespace Snakk.Api.Tests.Endpoints;

public class ManageContextEndpointTests : IAsyncDisposable
{
    private readonly TestWebServer _server = new();

    private const string TestUserId = "test-user-id";
    private const string CommunitySlug = "test-community";
    private const string CommunityName = "Test Community";
    private const string HubSlug = "test-hub";
    private const string HubName = "Test Hub";
    private const string SpaceSlug = "test-space";
    private const string SpaceName = "Test Space";

    /// <summary>
    /// Seeds a user with GlobalAdmin role, a community, a hub, and a space
    /// into the InMemory database for happy-path tests.
    /// </summary>
    private async Task SeedDataAsync()
    {
        using var scope = _server.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SnakkDbContext>();

        var adminUser = new UserDatabaseEntity
        {
            Id = 1,
            PublicId = TestUserId,
            DisplayName = "Test Admin",
            Email = "admin@test.com",
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(adminUser);

        var community = new CommunityDatabaseEntity
        {
            Id = 1,
            PublicId = "community-public-id",
            Slug = CommunitySlug,
            Name = CommunityName,
            Description = "A test community",
            CreatedAt = DateTime.UtcNow,
            VisibilityId = 1
        };
        db.Communities.Add(community);

        var hub = new HubDatabaseEntity
        {
            Id = 1,
            PublicId = "hub-public-id",
            Slug = HubSlug,
            Name = HubName,
            Description = "A test hub",
            CommunityId = 1,
            CreatedAt = DateTime.UtcNow
        };
        db.Hubs.Add(hub);

        var space = new SpaceDatabaseEntity
        {
            Id = 1,
            PublicId = "space-public-id",
            Slug = SpaceSlug,
            Name = SpaceName,
            Description = "A test space",
            HubId = 1,
            CreatedAt = DateTime.UtcNow
        };
        db.Spaces.Add(space);

        // Assign GlobalAdmin role to the admin user
        var globalAdminRole = new UserRoleDatabaseEntity
        {
            Id = 1,
            PublicId = Guid.NewGuid().ToString(),
            UserId = adminUser.Id,
            RoleId = (int)UserRoleTypeEnum.GlobalAdmin,
            AssignedByUserId = adminUser.Id,
            AssignedAt = DateTime.UtcNow
        };
        db.UserRoles.Add(globalAdminRole);

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Seeds only a community (and optionally hub/space) without any user roles,
    /// so permission checks will deny access.
    /// </summary>
    private async Task SeedDataWithoutRolesAsync(bool includeHub = false, bool includeSpace = false)
    {
        using var scope = _server.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SnakkDbContext>();

        var user = new UserDatabaseEntity
        {
            Id = 1,
            PublicId = TestUserId,
            DisplayName = "Regular User",
            Email = "user@test.com",
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);

        var community = new CommunityDatabaseEntity
        {
            Id = 1,
            PublicId = "community-public-id",
            Slug = CommunitySlug,
            Name = CommunityName,
            Description = "A test community",
            CreatedAt = DateTime.UtcNow,
            VisibilityId = 1
        };
        db.Communities.Add(community);

        if (includeHub || includeSpace)
        {
            var hub = new HubDatabaseEntity
            {
                Id = 1,
                PublicId = "hub-public-id",
                Slug = HubSlug,
                Name = HubName,
                Description = "A test hub",
                CommunityId = 1,
                CreatedAt = DateTime.UtcNow
            };
            db.Hubs.Add(hub);

            if (includeSpace)
            {
                var space = new SpaceDatabaseEntity
                {
                    Id = 1,
                    PublicId = "space-public-id",
                    Slug = SpaceSlug,
                    Name = SpaceName,
                    Description = "A test space",
                    HubId = 1,
                    CreatedAt = DateTime.UtcNow
                };
                db.Spaces.Add(space);
            }
        }

        await db.SaveChangesAsync();
    }

    // ==================== Unauthenticated ====================

    [Test]
    public async Task ResolveScope_Unauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync($"/manage/resolve?communityId=community-public-id");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    // ==================== Missing required parameters ====================

    [Test]
    public async Task ResolveScope_MissingCommunityId_ReturnsBadRequest()
    {
        // Arrange — communityId is a required (non-nullable) string parameter,
        // so omitting it should result in a 400 Bad Request from model binding.
        var client = _server.CreateAuthenticatedClient(role: "Admin");

        // Act
        var response = await client.GetAsync("/manage/resolve");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    // ==================== Community scope ====================

    [Test]
    public async Task ResolveScope_CommunityOnly_WithAdmin_ReturnsCommunityScope()
    {
        // Arrange
        await SeedDataAsync();
        var client = _server.CreateAuthenticatedClient(role: "Admin");

        // Act
        var response = await client.GetAsync($"/manage/resolve?communityId=community-public-id");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        await Assert.That(json.RootElement.GetProperty("scopeType").GetString()).IsEqualTo("Community");
        await Assert.That(json.RootElement.GetProperty("scopeName").GetString()).IsEqualTo(CommunityName);
        await Assert.That(json.RootElement.GetProperty("communitySlug").GetString()).IsEqualTo(CommunitySlug);
        await Assert.That(json.RootElement.GetProperty("communityName").GetString()).IsEqualTo(CommunityName);
    }

    [Test]
    public async Task ResolveScope_CommunityOnly_NonExistentId_ReturnsNotFound()
    {
        // Arrange — seed data so the user exists and auth passes, but use a non-existent id
        await SeedDataAsync();
        var client = _server.CreateAuthenticatedClient(role: "Admin");

        // Act
        var response = await client.GetAsync("/manage/resolve?communityId=does-not-exist");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task ResolveScope_CommunityOnly_NoPermission_ReturnsForbidden()
    {
        // Arrange — user exists but has no roles, so permission check denies access
        await SeedDataWithoutRolesAsync();
        var client = _server.CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync($"/manage/resolve?communityId=community-public-id");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    // ==================== Hub scope ====================

    [Test]
    public async Task ResolveScope_WithHub_WithAdmin_ReturnsHubScope()
    {
        // Arrange
        await SeedDataAsync();
        var client = _server.CreateAuthenticatedClient(role: "Admin");

        // Act
        var response = await client.GetAsync(
            $"/manage/resolve?communityId=community-public-id&hubId=hub-public-id");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        await Assert.That(json.RootElement.GetProperty("scopeType").GetString()).IsEqualTo("Hub");
        await Assert.That(json.RootElement.GetProperty("scopeName").GetString()).IsEqualTo(HubName);
        await Assert.That(json.RootElement.GetProperty("communitySlug").GetString()).IsEqualTo(CommunitySlug);
        await Assert.That(json.RootElement.GetProperty("hubSlug").GetString()).IsEqualTo(HubSlug);
        await Assert.That(json.RootElement.GetProperty("communityName").GetString()).IsEqualTo(CommunityName);
        await Assert.That(json.RootElement.GetProperty("hubName").GetString()).IsEqualTo(HubName);
    }

    [Test]
    public async Task ResolveScope_WithHub_NonExistentHub_ReturnsNotFound()
    {
        // Arrange — community exists but hub id does not
        await SeedDataAsync();
        var client = _server.CreateAuthenticatedClient(role: "Admin");

        // Act
        var response = await client.GetAsync(
            $"/manage/resolve?communityId=community-public-id&hubId=nonexistent-hub");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task ResolveScope_WithHub_NoPermission_ReturnsForbidden()
    {
        // Arrange — user exists with community and hub seeded, but no roles
        await SeedDataWithoutRolesAsync(includeHub: true);
        var client = _server.CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync(
            $"/manage/resolve?communityId=community-public-id&hubId=hub-public-id");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    // ==================== Space scope ====================

    [Test]
    public async Task ResolveScope_WithSpace_WithAdmin_ReturnsSpaceScope()
    {
        // Arrange
        await SeedDataAsync();
        var client = _server.CreateAuthenticatedClient(role: "Admin");

        // Act
        var response = await client.GetAsync(
            $"/manage/resolve?communityId=community-public-id&hubId=hub-public-id&spaceId=space-public-id");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        await Assert.That(json.RootElement.GetProperty("scopeType").GetString()).IsEqualTo("Space");
        await Assert.That(json.RootElement.GetProperty("scopeName").GetString()).IsEqualTo(SpaceName);
        await Assert.That(json.RootElement.GetProperty("communitySlug").GetString()).IsEqualTo(CommunitySlug);
        await Assert.That(json.RootElement.GetProperty("hubSlug").GetString()).IsEqualTo(HubSlug);
        await Assert.That(json.RootElement.GetProperty("spaceSlug").GetString()).IsEqualTo(SpaceSlug);
        await Assert.That(json.RootElement.GetProperty("communityName").GetString()).IsEqualTo(CommunityName);
        await Assert.That(json.RootElement.GetProperty("hubName").GetString()).IsEqualTo(HubName);
        await Assert.That(json.RootElement.GetProperty("spaceName").GetString()).IsEqualTo(SpaceName);
    }

    [Test]
    public async Task ResolveScope_WithSpace_NonExistentSpace_ReturnsNotFound()
    {
        // Arrange — community and hub exist but space id does not
        await SeedDataAsync();
        var client = _server.CreateAuthenticatedClient(role: "Admin");

        // Act
        var response = await client.GetAsync(
            $"/manage/resolve?communityId=community-public-id&hubId=hub-public-id&spaceId=nonexistent-space");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    // ==================== Permissions ====================

    [Test]
    public async Task ResolveScope_CommunityScope_IncludesPermissions()
    {
        // Arrange — GlobalAdmin should get all 7 permissions
        await SeedDataAsync();
        var client = _server.CreateAuthenticatedClient(role: "Admin");

        // Act
        var response = await client.GetAsync($"/manage/resolve?communityId=community-public-id");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        var permissions = json.RootElement.GetProperty("permissions");
        await Assert.That(permissions.ValueKind).IsEqualTo(JsonValueKind.Array);
        await Assert.That(permissions.GetArrayLength()).IsGreaterThanOrEqualTo(1);

        // Collect all permission strings from the response
        var permissionStrings = new List<string>();
        foreach (var item in permissions.EnumerateArray())
        {
            var permStr = item.GetString();
            if (permStr is not null) permissionStrings.Add(permStr);
        }

        // GlobalAdmin should have all manage permissions
        await Assert.That(permissionStrings).Contains("ViewDashboard");
        await Assert.That(permissionStrings).Contains("ManageContent");
        await Assert.That(permissionStrings).Contains("ManageReports");
        await Assert.That(permissionStrings).Contains("ManageBans");
        await Assert.That(permissionStrings).Contains("ManageSettings");
        await Assert.That(permissionStrings).Contains("ManageTeam");
        await Assert.That(permissionStrings).Contains("ManageWebhooks");
    }

    [Test]
    public async Task ResolveScope_GlobalAdmin_HasAccess()
    {
        // Arrange — GlobalAdmin should have access to any scope level
        await SeedDataAsync();
        var client = _server.CreateAuthenticatedClient(role: "Admin");

        // Act — test all three scope levels to verify GlobalAdmin passes all permission checks
        var communityResponse = await client.GetAsync(
            $"/manage/resolve?communityId=community-public-id");
        var hubResponse = await client.GetAsync(
            $"/manage/resolve?communityId=community-public-id&hubId=hub-public-id");
        var spaceResponse = await client.GetAsync(
            $"/manage/resolve?communityId=community-public-id&hubId=hub-public-id&spaceId=space-public-id");

        // Assert — all three should succeed
        await Assert.That(communityResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(hubResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(spaceResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task ResolveScope_HubScope_IncludesBreadcrumbs()
    {
        // Arrange — verify hub scope response includes parent community breadcrumb data
        await SeedDataAsync();
        var client = _server.CreateAuthenticatedClient(role: "Admin");

        // Act
        var response = await client.GetAsync(
            $"/manage/resolve?communityId=community-public-id&hubId=hub-public-id");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        // Hub scope should include community breadcrumb data
        await Assert.That(json.RootElement.GetProperty("communityName").GetString()).IsEqualTo(CommunityName);
        await Assert.That(json.RootElement.GetProperty("hubName").GetString()).IsEqualTo(HubName);

        // Space fields should be null at hub scope
        await Assert.That(json.RootElement.GetProperty("spaceSlug").ValueKind).IsEqualTo(JsonValueKind.Null);
        await Assert.That(json.RootElement.GetProperty("spaceName").ValueKind).IsEqualTo(JsonValueKind.Null);
    }

    public async ValueTask DisposeAsync()
    {
        await _server.DisposeAsync();
    }
}
