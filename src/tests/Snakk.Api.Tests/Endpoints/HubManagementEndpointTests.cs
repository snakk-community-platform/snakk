using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Snakk.Api.Tests.Helpers;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Shared.Enums;

namespace Snakk.Api.Tests.Endpoints;

public class HubManagementEndpointTests : IAsyncDisposable
{
    private readonly TestWebServer _server = new();

    private const string TestCommunitySlug = "test-community";
    private const string TestHubSlug = "test-hub";
    private const string TestUserId = "test-user-id";

    /// <summary>
    /// Seeds a community, a hub, an admin user with GlobalAdmin role
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
            Slug = TestHubSlug,
            Name = "Test Hub",
            Description = "A test hub for integration tests",
            CommunityId = community.Id,
            CreatedAt = DateTime.UtcNow
        };
        db.Hubs.Add(hub);

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
        var response = await client.GetAsync($"/api/c/{TestCommunitySlug}/h/{TestHubSlug}/manage/overview");

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
        var response = await client.GetAsync($"/api/c/{TestCommunitySlug}/h/{TestHubSlug}/manage/overview");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        await Assert.That(json.RootElement.TryGetProperty("slug", out _)).IsTrue();
        await Assert.That(json.RootElement.TryGetProperty("name", out _)).IsTrue();
        await Assert.That(json.RootElement.TryGetProperty("totalSpaces", out _)).IsTrue();
        await Assert.That(json.RootElement.TryGetProperty("totalDiscussions", out _)).IsTrue();
        await Assert.That(json.RootElement.TryGetProperty("totalPosts", out _)).IsTrue();
    }

    [Test]
    public async Task GetOverview_NonExistentHub_ReturnsNotFound()
    {
        // Arrange — seed admin user so authorization passes, but use a non-existent hub slug
        await SeedDataAsync();
        var client = _server.CreateAuthenticatedClient(role: "Admin");

        // Act
        var response = await client.GetAsync($"/api/c/{TestCommunitySlug}/h/does-not-exist/manage/overview");

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
        var response = await client.GetAsync($"/api/c/{TestCommunitySlug}/h/{TestHubSlug}/manage/settings");

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
        var response = await client.GetAsync($"/api/c/{TestCommunitySlug}/h/{TestHubSlug}/manage/settings");

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
        var request = new { Name = "Updated Hub Name", Description = "Updated description" };

        // Act
        var response = await client.PutAsJsonAsync($"/api/c/{TestCommunitySlug}/h/{TestHubSlug}/manage/settings", request);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task UpdateSettings_WithAdmin_ReturnsOk()
    {
        // Arrange
        await SeedDataAsync();
        var client = _server.CreateAuthenticatedClient(role: "Admin");
        var request = new { Name = "Updated Hub Name", Description = "Updated description" };

        // Act
        var response = await client.PutAsJsonAsync($"/api/c/{TestCommunitySlug}/h/{TestHubSlug}/manage/settings", request);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        await Assert.That(json.RootElement.GetProperty("name").GetString()).IsEqualTo("Updated Hub Name");
    }

    // ==================== GET Moderation ====================

    [Test]
    public async Task GetModeration_Unauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync($"/api/c/{TestCommunitySlug}/h/{TestHubSlug}/manage/moderation");

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
        var response = await client.GetAsync($"/api/c/{TestCommunitySlug}/h/{TestHubSlug}/manage/moderation");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        await Assert.That(json.RootElement.TryGetProperty("pendingReports", out _)).IsTrue();
        await Assert.That(json.RootElement.TryGetProperty("stats", out _)).IsTrue();
    }

    // ==================== GET Spaces ====================

    [Test]
    public async Task GetSpaces_Unauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync($"/api/c/{TestCommunitySlug}/h/{TestHubSlug}/manage/spaces");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetSpaces_WithAdmin_ReturnsOk()
    {
        // Arrange
        await SeedDataAsync();
        var client = _server.CreateAuthenticatedClient(role: "Admin");

        // Act
        var response = await client.GetAsync($"/api/c/{TestCommunitySlug}/h/{TestHubSlug}/manage/spaces");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        await Assert.That(json.RootElement.TryGetProperty("spaces", out _)).IsTrue();
        await Assert.That(json.RootElement.TryGetProperty("total", out _)).IsTrue();
    }

    public async ValueTask DisposeAsync()
    {
        await _server.DisposeAsync();
    }
}
