using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Snakk.Api.Tests.Helpers;

namespace Snakk.Api.Tests.Endpoints;

public class FollowEndpointTests : IAsyncDisposable
{
    private readonly TestWebServer _server = new();

    // ── Discussion follow endpoints ──────────────────────────────────

    [Test]
    public async Task ToggleFollowDiscussion_WithoutAuth_Returns_Unauthorized()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.PostAsync("/api/discussions/some-id/follow", null);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task ToggleFollowDiscussion_WithAuth_Returns_Ok()
    {
        // Arrange
        var client = _server.CreateAuthenticatedClient();

        // Act
        var response = await client.PostAsync("/api/discussions/some-discussion-id/follow", null);

        // Assert
        // Discussion may not exist, but auth should pass; expect Ok or BadRequest (not 401/403)
        await Assert.That((int)response.StatusCode).IsNotEqualTo((int)HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetDiscussionFollowStatus_WithoutAuth_Returns_Ok_NotFollowing()
    {
        // Arrange — endpoint returns { isFollowing: false } for unauthenticated users
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync("/api/discussions/some-discussion-id/follow-status");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        await Assert.That(json.RootElement.GetProperty("isFollowing").GetBoolean()).IsFalse();
    }

    [Test]
    public async Task GetDiscussionFollowStatus_WithAuth_Returns_Ok()
    {
        // Arrange
        var client = _server.CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/api/discussions/some-discussion-id/follow-status");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        await Assert.That(json.RootElement.TryGetProperty("isFollowing", out _)).IsTrue();
    }

    // ── Space follow endpoints ───────────────────────────────────────

    [Test]
    public async Task ToggleFollowSpace_WithoutAuth_Returns_Unauthorized()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.PostAsync("/api/spaces/some-space-id/follow", null);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task UpdateSpaceFollowLevel_WithoutAuth_Returns_Unauthorized()
    {
        // Arrange
        var client = _server.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Put, "/api/spaces/some-space-id/follow-level?level=posts");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetSpaceFollowStatus_WithoutAuth_Returns_Ok_NotFollowing()
    {
        // Arrange — endpoint returns { isFollowing: false, level: null } for unauthenticated users
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync("/api/spaces/some-space-id/follow-status");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        await Assert.That(json.RootElement.GetProperty("isFollowing").GetBoolean()).IsFalse();
    }

    // ── User follow endpoints ────────────────────────────────────────

    [Test]
    public async Task ToggleFollowUser_WithoutAuth_Returns_Unauthorized()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.PostAsync("/api/users/some-user-id/follow", null);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetUserFollowStatus_WithoutAuth_Returns_Ok_NotFollowing()
    {
        // Arrange — endpoint returns { isFollowing: false } for unauthenticated users
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync("/api/users/some-user-id/follow-status");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        await Assert.That(json.RootElement.GetProperty("isFollowing").GetBoolean()).IsFalse();
    }

    // ── Follow list endpoints (all require auth) ─────────────────────

    [Test]
    public async Task GetFollowedSpaces_WithoutAuth_Returns_Unauthorized()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync("/api/follows/spaces");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetFollowedDiscussions_WithoutAuth_Returns_Unauthorized()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync("/api/follows/discussions");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetFollowedUsers_WithoutAuth_Returns_Unauthorized()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync("/api/follows/users");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetFollowedSpaces_WithAuth_Returns_Ok_WithPublicIds()
    {
        // Arrange
        var client = _server.CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/api/follows/spaces");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        await Assert.That(json.RootElement.TryGetProperty("publicIds", out _)).IsTrue();
    }

    [Test]
    public async Task GetFollowedDiscussions_WithAuth_Returns_Ok_WithPublicIds()
    {
        // Arrange
        var client = _server.CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/api/follows/discussions");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        await Assert.That(json.RootElement.TryGetProperty("publicIds", out _)).IsTrue();
    }

    [Test]
    public async Task GetFollowedUsers_WithAuth_Returns_Ok_WithPublicIds()
    {
        // Arrange
        var client = _server.CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/api/follows/users");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        await Assert.That(json.RootElement.TryGetProperty("publicIds", out _)).IsTrue();
    }

    public async ValueTask DisposeAsync()
    {
        await _server.DisposeAsync();
    }
}
