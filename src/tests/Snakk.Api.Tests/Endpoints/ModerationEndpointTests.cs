using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Snakk.Api.Tests.Helpers;

namespace Snakk.Api.Tests.Endpoints;

public class ModerationEndpointTests : IAsyncDisposable
{
    private readonly TestWebServer _server = new();

    // ==================== Role Management ====================

    [Test]
    public async Task AssignRole_WithoutAuth_Returns_Unauthorized()
    {
        // Arrange
        var client = _server.CreateClient();
        var request = new
        {
            targetUserId = "some-user-id",
            roleType = 1,
            communityId = (string?)null,
            hubId = (string?)null,
            spaceId = (string?)null
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/moderation/roles", request);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task RevokeRole_WithoutAuth_Returns_Unauthorized()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.DeleteAsync("/api/moderation/roles/some-role-id");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetUserRoles_WithAuth_Returns_200()
    {
        // Arrange
        var client = _server.CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/api/moderation/users/some-user-id/roles");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        await Assert.That(json.RootElement.TryGetProperty("items", out _)).IsTrue();
    }

    // ==================== Ban Management ====================

    [Test]
    public async Task BanUser_WithoutAuth_Returns_Unauthorized()
    {
        // Arrange
        var client = _server.CreateClient();
        var request = new
        {
            targetUserId = "some-user-id",
            banType = 1,
            reason = "Test ban"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/moderation/bans", request);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task UnbanUser_WithoutAuth_Returns_Unauthorized()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.DeleteAsync("/api/moderation/bans/some-ban-id");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task CheckUserBanned_Returns_200_WithBanStatus()
    {
        // Arrange — this endpoint allows anonymous access
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync("/api/moderation/users/some-user-id/banned");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        await Assert.That(json.RootElement.TryGetProperty("isBanned", out _)).IsTrue();
        await Assert.That(json.RootElement.GetProperty("isBanned").GetBoolean()).IsFalse();
    }

    // ==================== Report Management ====================

    [Test]
    public async Task CreateReport_WithoutAuth_Returns_Unauthorized()
    {
        // Arrange
        var client = _server.CreateClient();
        var request = new
        {
            postId = "some-post-id",
            reasonId = "some-reason-id",
            details = "This is a test report."
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/moderation/reports", request);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetReports_WithoutAuth_Returns_Unauthorized()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync("/api/moderation/reports?offset=0&pageSize=10");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task ResolveReport_WithoutAuth_Returns_Unauthorized()
    {
        // Arrange
        var client = _server.CreateClient();
        var request = new { resolutionNote = "Resolved" };

        // Act
        var response = await client.PostAsJsonAsync("/api/moderation/reports/some-id/resolve", request);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task DismissReport_WithoutAuth_Returns_Unauthorized()
    {
        // Arrange
        var client = _server.CreateClient();
        var request = new { resolutionNote = "Dismissed" };

        // Act
        var response = await client.PostAsJsonAsync("/api/moderation/reports/some-id/dismiss", request);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetReportReasons_Returns_200()
    {
        // Arrange — this endpoint allows anonymous access
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync("/api/moderation/reports/reasons");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        await Assert.That(json.RootElement.TryGetProperty("items", out _)).IsTrue();
    }

    // ==================== Content Moderation ====================

    [Test]
    public async Task ModeratorDeletePost_WithoutAuth_Returns_Unauthorized()
    {
        // Arrange
        var client = _server.CreateClient();
        var request = new { reason = "Spam" };

        // Act
        var response = await client.PostAsJsonAsync("/api/moderation/posts/some-post-id/delete", request);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task LockDiscussion_WithoutAuth_Returns_Unauthorized()
    {
        // Arrange
        var client = _server.CreateClient();
        var request = new { reason = "Off-topic" };

        // Act
        var response = await client.PostAsJsonAsync("/api/moderation/discussions/some-id/lock", request);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task UnlockDiscussion_WithoutAuth_Returns_Unauthorized()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.PostAsync("/api/moderation/discussions/some-id/unlock", null);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    // ==================== Moderation Log ====================

    [Test]
    public async Task GetModerationLog_WithoutAuth_Returns_Unauthorized()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync("/api/moderation/log?offset=0&pageSize=10");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    public async ValueTask DisposeAsync()
    {
        await _server.DisposeAsync();
    }
}
