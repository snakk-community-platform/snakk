using System.Net;
using System.Text.Json;
using Snakk.Api.Tests.Helpers;

namespace Snakk.Api.Tests.Endpoints;

public class StatsEndpointTests : IAsyncDisposable
{
    private readonly TestWebServer _server = new();

    // ==================== Platform Stats ====================

    [Test]
    public async Task GetPlatformStats_Returns_200_WithStats()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync("/platform/stats");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        await Assert.That(json.RootElement.TryGetProperty("hubCount", out _)).IsTrue();
        await Assert.That(json.RootElement.TryGetProperty("spaceCount", out _)).IsTrue();
        await Assert.That(json.RootElement.TryGetProperty("discussionCount", out _)).IsTrue();
        await Assert.That(json.RootElement.TryGetProperty("replyCount", out _)).IsTrue();
    }

    // ==================== Hub Stats ====================

    [Test]
    public async Task GetHubStats_WithNonExistentId_Returns_NotFound()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync("/hubs/non-existent-id/stats");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    // ==================== Space Stats ====================

    [Test]
    public async Task GetSpaceStats_WithNonExistentId_Returns_NotFound()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync("/spaces/non-existent-id/stats");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    // ==================== Community Stats ====================

    [Test]
    public async Task GetCommunityStats_WithNonExistentId_Returns_NotFound()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync("/communities/non-existent-id/stats");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    // ==================== User Stats ====================

    [Test]
    public async Task GetUserStats_WithNonExistentId_Returns_NotFound()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync("/users/non-existent-id/stats");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    // ==================== Discussion Stats ====================

    [Test]
    public async Task GetDiscussionStats_WithNonExistentId_Returns_NotFound()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync("/discussions/non-existent-id/stats");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    // ==================== Stats Are Public (No Auth Required) ====================

    [Test]
    public async Task PlatformStats_NoAuthRequired_Returns_200()
    {
        // Arrange — explicitly using unauthenticated client
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync("/platform/stats");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task HubStats_NoAuthRequired_Returns_NotFound_ForMissingHub()
    {
        // Arrange — explicitly using unauthenticated client to verify no 401
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync("/hubs/missing-hub/stats");

        // Assert — should be NotFound (entity missing), NOT Unauthorized
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    public async ValueTask DisposeAsync()
    {
        await _server.DisposeAsync();
    }
}
