using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Snakk.Api.Tests.Helpers;

namespace Snakk.Api.Tests.Endpoints;

public class ReactionEndpointTests : IAsyncDisposable
{
    private readonly TestWebServer _server = new();

    // ── Toggle reaction (POST, requires auth) ────────────────────────

    [Test]
    public async Task ToggleReaction_WithoutAuth_Returns_Unauthorized()
    {
        // Arrange
        var client = _server.CreateClient();
        var body = new { type = "Agree" };

        // Act
        var response = await client.PostAsJsonAsync("/posts/some-post-id/reactions/", body);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task ToggleReaction_WithAuth_Returns_NonUnauthorized()
    {
        // Arrange
        var client = _server.CreateAuthenticatedClient();
        var body = new { type = "Agree" };

        // Act
        var response = await client.PostAsJsonAsync("/posts/some-post-id/reactions/", body);

        // Assert
        // Post may not exist, but auth should pass; we should not get 401
        await Assert.That((int)response.StatusCode).IsNotEqualTo((int)HttpStatusCode.Unauthorized);
    }

    // ── Get reaction counts (GET, public) ────────────────────────────

    [Test]
    public async Task GetReactionCounts_WithoutAuth_Returns_Ok()
    {
        // Arrange — public endpoint, no auth required
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync("/posts/some-post-id/reactions/");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task GetReactionCounts_Returns_ExpectedShape()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync("/posts/some-post-id/reactions/");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        await Assert.That(json.RootElement.TryGetProperty("counts", out _)).IsTrue();
    }

    [Test]
    public async Task GetReactionCounts_ForNonExistentPost_Returns_EmptyCounts()
    {
        // Arrange — non-existent post should return empty counts dictionary
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync("/posts/non-existent-post/reactions/");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        var counts = json.RootElement.GetProperty("counts");
        await Assert.That(counts.ValueKind).IsEqualTo(JsonValueKind.Object);
    }

    // ── Get my reactions (GET, auth-aware) ────────────────────────────

    [Test]
    public async Task GetMyReactions_WithoutAuth_Returns_Ok_EmptyReactions()
    {
        // Arrange — returns { reactions: [] } for unauthenticated users
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync("/posts/some-post-id/reactions/me");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        await Assert.That(json.RootElement.TryGetProperty("reactions", out _)).IsTrue();
        await Assert.That(json.RootElement.GetProperty("reactions").GetArrayLength()).IsEqualTo(0);
    }

    [Test]
    public async Task GetMyReactions_WithAuth_Returns_Ok()
    {
        // Arrange
        var client = _server.CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/posts/some-post-id/reactions/me");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        await Assert.That(json.RootElement.TryGetProperty("reactions", out _)).IsTrue();
    }

    public async ValueTask DisposeAsync()
    {
        await _server.DisposeAsync();
    }
}
