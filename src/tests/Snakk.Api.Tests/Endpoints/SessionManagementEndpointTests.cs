using System.Net;
using System.Text.Json;
using Microsoft.Net.Http.Headers;
using Snakk.Api.Tests.Helpers;

namespace Snakk.Api.Tests.Endpoints;

public class SessionManagementEndpointTests : IAsyncDisposable
{
    private readonly TestWebServer _server = new();

    // ── GET /api/sessions ───────────────────────────────────────────────

    [Test]
    public async Task GetActiveSessions_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync("/sessions");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetActiveSessions_WithAuth_ReturnsOk()
    {
        // Arrange
        var client = _server.CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/sessions");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        await Assert.That(json.RootElement.TryGetProperty("totalSessions", out _)).IsTrue();
        await Assert.That(json.RootElement.TryGetProperty("sessions", out _)).IsTrue();
    }

    // ── DELETE /api/sessions/{sessionId} ────────────────────────────────

    [Test]
    public async Task RevokeSession_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.DeleteAsync("/sessions/nonexistent-session-id");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task RevokeSession_WithNonexistentSession_ReturnsNotFound()
    {
        // Arrange
        var client = _server.CreateAuthenticatedClient();

        // Act
        var response = await client.DeleteAsync("/sessions/nonexistent-session-id");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        await Assert.That(json.RootElement.TryGetProperty("error", out _)).IsTrue();
    }

    // ── POST /api/sessions/revoke-all ───────────────────────────────────

    [Test]
    public async Task RevokeAllSessions_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.PostAsync("/sessions/revoke-all", null);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task RevokeAllSessions_WithAuth_ReturnsOk()
    {
        // Arrange
        var client = _server.CreateAuthenticatedClient();

        // Act
        var response = await client.PostAsync("/sessions/revoke-all", null);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        await Assert.That(json.RootElement.TryGetProperty("message", out _)).IsTrue();
    }

    // ── POST /api/sessions/refresh ──────────────────────────────────────

    [Test]
    public async Task RefreshToken_WithoutCookie_ReturnsUnauthorized()
    {
        // Arrange - no cookie, no auth header
        var client = _server.CreateClient();

        // Act
        var response = await client.PostAsync("/sessions/refresh", null);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task RefreshToken_WithInvalidToken_ReturnsUnauthorized()
    {
        // Arrange - send a bogus refresh_token cookie
        var client = _server.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/sessions/refresh");
        request.Headers.Add("Cookie", "refresh_token=invalid-bogus-token-value");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    public async ValueTask DisposeAsync()
    {
        await _server.DisposeAsync();
    }
}
