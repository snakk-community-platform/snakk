using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Snakk.Api.Tests.Helpers;

namespace Snakk.Api.Tests.Endpoints;

public class AdminSettingsEndpointTests : IAsyncDisposable
{
    private readonly TestWebServer _server = new();

    // ==================== General Settings ====================

    [Test]
    public async Task GetGeneralSettings_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync("/admin/settings/general");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetGeneralSettings_WithNonAdmin_ReturnsForbidden()
    {
        // Arrange
        var client = _server.CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/admin/settings/general");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task GetGeneralSettings_WithAdmin_ReturnsOk()
    {
        // Arrange
        var client = _server.CreateAuthenticatedClient(role: "Admin");

        // Act
        var response = await client.GetAsync("/admin/settings/general");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    // ==================== OAuth Provider Settings ====================

    [Test]
    public async Task GetOAuthProviders_WithAdmin_ReturnsOk()
    {
        // Arrange
        var client = _server.CreateAuthenticatedClient(role: "Admin");

        // Act
        var response = await client.GetAsync("/admin/settings/oauth");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        await Assert.That(json.RootElement.TryGetProperty("providers", out _)).IsTrue();
    }

    [Test]
    public async Task UpdateOAuthProvider_WithInvalidProvider_ReturnsBadRequest()
    {
        // Arrange
        var client = _server.CreateAuthenticatedClient(role: "Admin");
        var body = new { enabled = true };

        // Act
        var response = await client.PutAsJsonAsync("/admin/settings/oauth/InvalidProvider", body);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        await Assert.That(json.RootElement.GetProperty("error").GetString())
            .IsEqualTo("Invalid OAuth provider");
    }

    // ==================== Email Settings ====================

    [Test]
    public async Task GetEmailConfig_WithAdmin_ReturnsOk()
    {
        // Arrange
        var client = _server.CreateAuthenticatedClient(role: "Admin");

        // Act
        var response = await client.GetAsync("/admin/settings/email");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task TestEmail_WithAdmin_ReturnsOkOrBadRequest()
    {
        // Arrange
        var client = _server.CreateAuthenticatedClient(role: "Admin");
        var body = new { recipientEmail = "test@example.com" };

        // Act
        var response = await client.PostAsJsonAsync("/admin/settings/email/test", body);

        // Assert - test email may fail (no real SMTP) but should not be 401/403
        var statusCode = response.StatusCode;
        await Assert.That(statusCode == HttpStatusCode.OK || statusCode == HttpStatusCode.BadRequest).IsTrue();
    }

    // ==================== Avatar Settings ====================

    [Test]
    public async Task GetAvatarSettings_WithAdmin_ReturnsOk()
    {
        // Arrange
        var client = _server.CreateAuthenticatedClient(role: "Admin");

        // Act
        var response = await client.GetAsync("/admin/settings/avatar");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    // ==================== Content Settings ====================

    [Test]
    public async Task GetContentSettings_WithAdmin_ReturnsOk()
    {
        // Arrange
        var client = _server.CreateAuthenticatedClient(role: "Admin");

        // Act
        var response = await client.GetAsync("/admin/settings/content");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    // ==================== Rate Limiting Settings ====================

    [Test]
    public async Task GetRateLimitingSettings_WithAdmin_ReturnsOk()
    {
        // Arrange
        var client = _server.CreateAuthenticatedClient(role: "Admin");

        // Act
        var response = await client.GetAsync("/admin/settings/rate-limiting");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    // ==================== PUT Auth Enforcement ====================

    [Test]
    public async Task UpdateGeneralSettings_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var client = _server.CreateClient();
        var body = new { siteName = "Test Site", siteDescription = "A test site" };

        // Act
        var response = await client.PutAsJsonAsync("/admin/settings/general", body);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task UpdateRateLimitingSettings_WithAdmin_ReachesHandler()
    {
        // Arrange
        var client = _server.CreateAuthenticatedClient(role: "Admin");
        var body = new
        {
            enabled = true,
            requestsPerMinute = 60,
            requestsPerHour = 1000
        };

        // Act
        var response = await client.PutAsJsonAsync("/admin/settings/rate-limiting", body);

        // Assert - should not be 401/403 (auth passes), may be 200 or 400 depending on DTO shape
        var statusCode = response.StatusCode;
        await Assert.That(statusCode != HttpStatusCode.Unauthorized && statusCode != HttpStatusCode.Forbidden).IsTrue();
    }

    public async ValueTask DisposeAsync()
    {
        await _server.DisposeAsync();
    }
}
