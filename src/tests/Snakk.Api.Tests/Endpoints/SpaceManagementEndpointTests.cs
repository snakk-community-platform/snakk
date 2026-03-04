using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Snakk.Api.Tests.Helpers;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Shared.Enums;

namespace Snakk.Api.Tests.Endpoints;

public class SpaceManagementEndpointTests : IAsyncDisposable
{
    private readonly TestWebServer _server = new();

    private const string TestCommunitySlug = "test-community";
    private const string TestSpaceSlug = "test-space";
    private const string TestUserId = "test-user-id";

    /// <summary>
    /// Seeds a community, hub, space, and an admin user with GlobalAdmin role
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
            Slug = TestCommunitySlug,
            Name = "Test Community",
            Description = "A test community for integration tests",
            CreatedAt = DateTime.UtcNow,
            VisibilityId = 1
        };
        db.Communities.Add(community);

        var hub = new HubDatabaseEntity
        {
            Id = 1,
            PublicId = "hub-public-id",
            Slug = "test-hub",
            Name = "Test Hub",
            Description = "A test hub",
            CommunityId = 1,
            CreatedAt = DateTime.UtcNow
        };
        db.Hubs.Add(hub);

        var space = new SpaceDatabaseEntity
        {
            Id = 1,
            PublicId = "space-public-id",
            Slug = TestSpaceSlug,
            Name = "Test Space",
            Description = "A test space for integration tests",
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

    // ==================== GET Overview ====================

    [Test]
    public async Task GetOverview_Unauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync($"/spaces/space-public-id/manage/overview");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetOverview_WithAdmin_ReturnsOk()
    {
        // Arrange
        await SeedDataAsync();
        var client = _server.CreateAuthenticatedClient(role: "Admin");

        // Act
        var response = await client.GetAsync($"/spaces/space-public-id/manage/overview");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task GetOverview_NonExistentSpace_ReturnsNotFound()
    {
        // Arrange — seed admin user so authorization passes, but use a non-existent space id
        await SeedDataAsync();
        var client = _server.CreateAuthenticatedClient(role: "Admin");

        // Act
        var response = await client.GetAsync("/spaces/does-not-exist/manage/overview");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    // ==================== GET Settings ====================

    [Test]
    public async Task GetSettings_WithAdmin_ReturnsOk()
    {
        // Arrange
        await SeedDataAsync();
        var client = _server.CreateAuthenticatedClient(role: "Admin");

        // Act
        var response = await client.GetAsync($"/spaces/space-public-id/manage/settings");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        await Assert.That(json.RootElement.TryGetProperty("slug", out _)).IsTrue();
        await Assert.That(json.RootElement.TryGetProperty("name", out _)).IsTrue();
    }

    [Test]
    public async Task GetSettings_Unauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync($"/spaces/space-public-id/manage/settings");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    // ==================== PUT Settings ====================

    [Test]
    public async Task UpdateSettings_WithAdmin_ReturnsOk()
    {
        // Arrange
        await SeedDataAsync();
        var client = _server.CreateAuthenticatedClient(role: "Admin");
        var request = new
        {
            Name = "Updated Space Name",
            Description = "Updated description",
            AllowedThreadTypes = new[] { "Discussion", "Question" },
            RequireApproval = false,
            AllowAnonymous = false
        };

        // Act
        var response = await client.PutAsJsonAsync(
            $"/spaces/space-public-id/manage/settings", request);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        await Assert.That(json.RootElement.GetProperty("name").GetString()).IsEqualTo("Updated Space Name");
    }

    [Test]
    public async Task UpdateSettings_Unauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var client = _server.CreateClient();
        var request = new
        {
            Name = "Updated Space Name",
            Description = "Updated description",
            AllowedThreadTypes = new[] { "Discussion" },
            RequireApproval = false,
            AllowAnonymous = false
        };

        // Act
        var response = await client.PutAsJsonAsync(
            $"/spaces/space-public-id/manage/settings", request);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    // ==================== GET Moderation ====================

    [Test]
    public async Task GetModeration_WithAdmin_ReturnsOk()
    {
        // Arrange
        await SeedDataAsync();
        var client = _server.CreateAuthenticatedClient(role: "Admin");

        // Act
        var response = await client.GetAsync($"/spaces/space-public-id/manage/moderation");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task GetModeration_Unauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync($"/spaces/space-public-id/manage/moderation");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    // ==================== GET Rules ====================

    [Test]
    public async Task GetRules_WithAdmin_ReturnsOk()
    {
        // Arrange
        await SeedDataAsync();
        var client = _server.CreateAuthenticatedClient(role: "Admin");

        // Act
        var response = await client.GetAsync($"/spaces/space-public-id/manage/rules");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task GetRules_Unauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync($"/spaces/space-public-id/manage/rules");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    // ==================== PUT Rules ====================

    [Test]
    public async Task UpdateRules_WithAdmin_ReturnsOk()
    {
        // Arrange
        await SeedDataAsync();
        var client = _server.CreateAuthenticatedClient(role: "Admin");
        var request = new
        {
            Rules = new[]
            {
                new { Id = 0, Title = "Be respectful", Description = "Treat others with respect", Order = 1 },
                new { Id = 0, Title = "No spam", Description = "Do not post spam content", Order = 2 }
            }
        };

        // Act
        var response = await client.PutAsJsonAsync(
            $"/spaces/space-public-id/manage/rules", request);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task UpdateRules_Unauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var client = _server.CreateClient();
        var request = new
        {
            Rules = new[]
            {
                new { Id = 0, Title = "Be respectful", Description = "Treat others with respect", Order = 1 }
            }
        };

        // Act
        var response = await client.PutAsJsonAsync(
            $"/spaces/space-public-id/manage/rules", request);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    public async ValueTask DisposeAsync()
    {
        await _server.DisposeAsync();
    }
}
