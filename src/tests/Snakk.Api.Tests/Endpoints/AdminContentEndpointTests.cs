using System.Net;
using System.Text.Json;
using Snakk.Api.Tests.Helpers;

namespace Snakk.Api.Tests.Endpoints;

public class AdminContentEndpointTests : IAsyncDisposable
{
    private readonly TestWebServer _server = new();

    // ==================== Content Overview ====================

    [Test]
    public async Task GetContentOverview_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync("/admin/content/overview");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetContentOverview_WithNonAdmin_ReturnsForbidden()
    {
        // Arrange
        var client = _server.CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/admin/content/overview");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task GetContentOverview_WithAdmin_ReturnsOkWithCounts()
    {
        // Arrange
        var client = _server.CreateAuthenticatedClient(role: "Admin");

        // Act
        var response = await client.GetAsync("/admin/content/overview");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        await Assert.That(json.RootElement.TryGetProperty("totalCommunities", out _)).IsTrue();
        await Assert.That(json.RootElement.TryGetProperty("totalHubs", out _)).IsTrue();
        await Assert.That(json.RootElement.TryGetProperty("totalSpaces", out _)).IsTrue();
        await Assert.That(json.RootElement.TryGetProperty("totalDiscussions", out _)).IsTrue();
        await Assert.That(json.RootElement.TryGetProperty("totalPosts", out _)).IsTrue();
        await Assert.That(json.RootElement.TryGetProperty("pinnedDiscussions", out _)).IsTrue();
        await Assert.That(json.RootElement.TryGetProperty("lockedDiscussions", out _)).IsTrue();
    }

    // ==================== Communities ====================

    [Test]
    public async Task GetCommunities_WithAdmin_ReturnsOkWithPagination()
    {
        // Arrange
        var client = _server.CreateAuthenticatedClient(role: "Admin");

        // Act
        var response = await client.GetAsync("/admin/content/communities?page=1&pageSize=20");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        await Assert.That(json.RootElement.TryGetProperty("communities", out _)).IsTrue();
        await Assert.That(json.RootElement.TryGetProperty("total", out _)).IsTrue();
        await Assert.That(json.RootElement.TryGetProperty("page", out _)).IsTrue();
        await Assert.That(json.RootElement.TryGetProperty("pageSize", out _)).IsTrue();
    }

    [Test]
    public async Task GetCommunity_WithNonexistentId_ReturnsNotFound()
    {
        // Arrange
        var client = _server.CreateAuthenticatedClient(role: "Admin");

        // Act
        var response = await client.GetAsync("/admin/content/communities/nonexistent-id");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    // ==================== Hubs ====================

    [Test]
    public async Task GetHubs_WithAdmin_ReturnsOkWithPagination()
    {
        // Arrange
        var client = _server.CreateAuthenticatedClient(role: "Admin");

        // Act
        var response = await client.GetAsync("/admin/content/hubs?page=1&pageSize=20");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        await Assert.That(json.RootElement.TryGetProperty("hubs", out _)).IsTrue();
        await Assert.That(json.RootElement.TryGetProperty("total", out _)).IsTrue();
        await Assert.That(json.RootElement.TryGetProperty("page", out _)).IsTrue();
        await Assert.That(json.RootElement.TryGetProperty("pageSize", out _)).IsTrue();
    }

    // ==================== Spaces ====================

    [Test]
    public async Task GetSpaces_WithAdmin_ReturnsOkWithPagination()
    {
        // Arrange
        var client = _server.CreateAuthenticatedClient(role: "Admin");

        // Act
        var response = await client.GetAsync("/admin/content/spaces?page=1&pageSize=20");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        await Assert.That(json.RootElement.TryGetProperty("spaces", out _)).IsTrue();
        await Assert.That(json.RootElement.TryGetProperty("total", out _)).IsTrue();
        await Assert.That(json.RootElement.TryGetProperty("page", out _)).IsTrue();
        await Assert.That(json.RootElement.TryGetProperty("pageSize", out _)).IsTrue();
    }

    // ==================== Discussions ====================

    [Test]
    public async Task GetDiscussions_WithAdmin_ReturnsOkWithPagination()
    {
        // Arrange
        var client = _server.CreateAuthenticatedClient(role: "Admin");

        // Act
        var response = await client.GetAsync("/admin/content/discussions?page=1&pageSize=20");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        await Assert.That(json.RootElement.TryGetProperty("discussions", out _)).IsTrue();
        await Assert.That(json.RootElement.TryGetProperty("total", out _)).IsTrue();
        await Assert.That(json.RootElement.TryGetProperty("page", out _)).IsTrue();
        await Assert.That(json.RootElement.TryGetProperty("pageSize", out _)).IsTrue();
    }

    [Test]
    public async Task GetDiscussion_WithNonexistentId_ReturnsNotFound()
    {
        // Arrange
        var client = _server.CreateAuthenticatedClient(role: "Admin");

        // Act
        var response = await client.GetAsync("/admin/content/discussions/nonexistent-id");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    // ==================== Pin Discussion ====================

    [Test]
    public async Task PinDiscussion_WithNonexistentId_ReturnsNotFound()
    {
        // Arrange
        var client = _server.CreateAuthenticatedClient(role: "Admin");

        // Act
        var response = await client.PostAsync("/admin/content/discussions/nonexistent-id/pin", null);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task UnpinDiscussion_WithNonexistentId_ReturnsNotFound()
    {
        // Arrange
        var client = _server.CreateAuthenticatedClient(role: "Admin");

        // Act
        var response = await client.DeleteAsync("/admin/content/discussions/nonexistent-id/pin");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    // ==================== Lock Discussion ====================

    [Test]
    public async Task LockDiscussion_WithNonexistentId_ReturnsNotFound()
    {
        // Arrange
        var client = _server.CreateAuthenticatedClient(role: "Admin");

        // Act
        var response = await client.PostAsync("/admin/content/discussions/nonexistent-id/lock", null);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task UnlockDiscussion_WithNonexistentId_ReturnsNotFound()
    {
        // Arrange
        var client = _server.CreateAuthenticatedClient(role: "Admin");

        // Act
        var response = await client.DeleteAsync("/admin/content/discussions/nonexistent-id/lock");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    // ==================== Delete Discussion ====================

    [Test]
    public async Task DeleteDiscussion_WithNonexistentId_ReturnsNotFound()
    {
        // Arrange
        var client = _server.CreateAuthenticatedClient(role: "Admin");

        // Act
        var response = await client.DeleteAsync("/admin/content/discussions/nonexistent-id");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    public async ValueTask DisposeAsync()
    {
        await _server.DisposeAsync();
    }
}
