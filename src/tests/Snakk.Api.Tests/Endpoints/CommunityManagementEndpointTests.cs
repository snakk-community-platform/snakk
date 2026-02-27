using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Snakk.Api.Tests.Helpers;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Shared.Enums;

namespace Snakk.Api.Tests.Endpoints;

public class CommunityManagementEndpointTests : IAsyncDisposable
{
    private readonly TestWebServer _server = new();

    private const string TestSlug = "test-community";
    private const string TestUserId = "test-user-id";
    private const string TargetUserId = "target-user-id";

    /// <summary>
    /// Seeds a community, an admin user with GlobalAdmin role, and optionally a target user
    /// into the InMemory database for happy-path tests.
    /// </summary>
    private async Task SeedDataAsync(bool seedTargetUser = false)
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
            Slug = TestSlug,
            Name = "Test Community",
            Description = "A test community for integration tests",
            CreatedAt = DateTime.UtcNow,
            VisibilityId = 1
        };
        db.Communities.Add(community);

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

        if (seedTargetUser)
        {
            var targetUser = new UserDatabaseEntity
            {
                Id = 2,
                PublicId = TargetUserId,
                DisplayName = "Target User",
                Email = "target@test.com",
                EmailVerified = true,
                CreatedAt = DateTime.UtcNow
            };
            db.Users.Add(targetUser);
        }

        await db.SaveChangesAsync();
    }

    // ==================== GET Overview ====================

    [Test]
    public async Task GetOverview_Unauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync($"/api/c/{TestSlug}/manage/overview");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetOverview_RegularUser_ReturnsForbidden()
    {
        // Arrange — regular user has no roles in the database,
        // so the PermissionAuthorizationHandler will deny access.
        var client = _server.CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync($"/api/c/{TestSlug}/manage/overview");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task GetOverview_WithAdmin_ReturnsOk()
    {
        // Arrange
        await SeedDataAsync();
        var client = _server.CreateAuthenticatedClient(role: "Admin");

        // Act
        var response = await client.GetAsync($"/api/c/{TestSlug}/manage/overview");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        await Assert.That(json.RootElement.TryGetProperty("slug", out _)).IsTrue();
        await Assert.That(json.RootElement.TryGetProperty("name", out _)).IsTrue();
        await Assert.That(json.RootElement.TryGetProperty("totalHubs", out _)).IsTrue();
        await Assert.That(json.RootElement.TryGetProperty("totalSpaces", out _)).IsTrue();
        await Assert.That(json.RootElement.TryGetProperty("totalDiscussions", out _)).IsTrue();
        await Assert.That(json.RootElement.TryGetProperty("totalPosts", out _)).IsTrue();
    }

    [Test]
    public async Task GetOverview_NonExistentSlug_ReturnsNotFound()
    {
        // Arrange — seed admin user so authorization passes, but use a non-existent slug
        await SeedDataAsync();
        var client = _server.CreateAuthenticatedClient(role: "Admin");

        // Act
        var response = await client.GetAsync("/api/c/does-not-exist/manage/overview");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    // ==================== GET Settings ====================

    [Test]
    public async Task GetSettings_Unauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync($"/api/c/{TestSlug}/manage/settings");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetSettings_WithAdmin_ReturnsOk()
    {
        // Arrange
        await SeedDataAsync();
        var client = _server.CreateAuthenticatedClient(role: "Admin");

        // Act
        var response = await client.GetAsync($"/api/c/{TestSlug}/manage/settings");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        await Assert.That(json.RootElement.TryGetProperty("slug", out _)).IsTrue();
        await Assert.That(json.RootElement.TryGetProperty("name", out _)).IsTrue();
    }

    // ==================== PUT Settings ====================

    [Test]
    public async Task UpdateSettings_Unauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var client = _server.CreateClient();
        var request = new { Name = "Updated Name", Description = "Updated description" };

        // Act
        var response = await client.PutAsJsonAsync($"/api/c/{TestSlug}/manage/settings", request);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task UpdateSettings_WithAdmin_ReturnsOk()
    {
        // Arrange
        await SeedDataAsync();
        var client = _server.CreateAuthenticatedClient(role: "Admin");
        var request = new { Name = "Updated Community Name", Description = "Updated description" };

        // Act
        var response = await client.PutAsJsonAsync($"/api/c/{TestSlug}/manage/settings", request);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        await Assert.That(json.RootElement.GetProperty("name").GetString()).IsEqualTo("Updated Community Name");
    }

    // ==================== GET Moderation ====================

    [Test]
    public async Task GetModeration_Unauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync($"/api/c/{TestSlug}/manage/moderation");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetModeration_WithAdmin_ReturnsOk()
    {
        // Arrange
        await SeedDataAsync();
        var client = _server.CreateAuthenticatedClient(role: "Admin");

        // Act
        var response = await client.GetAsync($"/api/c/{TestSlug}/manage/moderation");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        await Assert.That(json.RootElement.TryGetProperty("pendingReports", out _)).IsTrue();
        await Assert.That(json.RootElement.TryGetProperty("stats", out _)).IsTrue();
    }

    // ==================== GET Members ====================

    [Test]
    public async Task GetMembers_WithAdmin_ReturnsPaged()
    {
        // Arrange
        await SeedDataAsync();
        var client = _server.CreateAuthenticatedClient(role: "Admin");

        // Act
        var response = await client.GetAsync($"/api/c/{TestSlug}/manage/members?page=1&pageSize=10");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        await Assert.That(json.RootElement.TryGetProperty("members", out _)).IsTrue();
        await Assert.That(json.RootElement.TryGetProperty("total", out _)).IsTrue();
        await Assert.That(json.RootElement.TryGetProperty("page", out _)).IsTrue();
        await Assert.That(json.RootElement.TryGetProperty("pageSize", out _)).IsTrue();
        await Assert.That(json.RootElement.GetProperty("page").GetInt32()).IsEqualTo(1);
        await Assert.That(json.RootElement.GetProperty("pageSize").GetInt32()).IsEqualTo(10);
    }

    // ==================== POST Members Role ====================

    [Test]
    public async Task UpdateMemberRole_WithAdmin_ReturnsOk()
    {
        // Arrange — seed both admin user and target user
        await SeedDataAsync(seedTargetUser: true);
        var client = _server.CreateAuthenticatedClient(role: "Admin");
        var request = new { Action = "add", Role = "Admin" };

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/c/{TestSlug}/manage/members/{TargetUserId}/role", request);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task UpdateMemberRole_NonExistentUser_ReturnsNotFound()
    {
        // Arrange
        await SeedDataAsync();
        var client = _server.CreateAuthenticatedClient(role: "Admin");
        var request = new { Action = "add", Role = "Admin" };

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/c/{TestSlug}/manage/members/nonexistent-user/role", request);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    // ==================== GET Spaces ====================

    [Test]
    public async Task GetSpaces_WithAdmin_ReturnsOk()
    {
        // Arrange
        await SeedDataAsync();
        var client = _server.CreateAuthenticatedClient(role: "Admin");

        // Act
        var response = await client.GetAsync($"/api/c/{TestSlug}/manage/spaces");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        // Endpoint returns a list of HubSpaceItemDto
        await Assert.That(json.RootElement.ValueKind).IsEqualTo(JsonValueKind.Array);
    }

    public async ValueTask DisposeAsync()
    {
        await _server.DisposeAsync();
    }
}
