using System.Net;
using System.Text.Json;
using Snakk.Api.Tests.Helpers;

namespace Snakk.Api.Tests.Endpoints;

public class AdminSecurityEndpointTests : IAsyncDisposable
{
    private readonly TestWebServer _server = new();

    // ==================== Audit Logs ====================

    [Test]
    public async Task GetAuditLogs_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync("/admin/security/audit-logs");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetAuditLogs_WithNonAdminAuth_ReturnsForbidden()
    {
        // Arrange
        var client = _server.CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/admin/security/audit-logs");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task GetAuditLogs_WithAdminAuth_ReturnsOk()
    {
        // Arrange
        var client = _server.CreateAuthenticatedClient(role: "GlobalAdmin");

        // Act
        var response = await client.GetAsync("/admin/security/audit-logs");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task GetAuditLog_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        var client = _server.CreateAuthenticatedClient(role: "GlobalAdmin");

        // Act
        var response = await client.GetAsync("/admin/security/audit-logs/nonexistent-id");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    // ==================== Failed Logins ====================

    [Test]
    public async Task GetFailedLogins_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync("/admin/security/failed-logins");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetFailedLogins_WithAdminAuth_ReturnsOk()
    {
        // Arrange
        var client = _server.CreateAuthenticatedClient(role: "GlobalAdmin");

        // Act
        var response = await client.GetAsync("/admin/security/failed-logins");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        await Assert.That(json.RootElement.TryGetProperty("failures", out _)).IsTrue();
        await Assert.That(json.RootElement.TryGetProperty("suspiciousIps", out _)).IsTrue();
        await Assert.That(json.RootElement.TryGetProperty("total", out _)).IsTrue();
        await Assert.That(json.RootElement.TryGetProperty("page", out _)).IsTrue();
        await Assert.That(json.RootElement.TryGetProperty("pageSize", out _)).IsTrue();
    }

    [Test]
    [Arguments(0)]
    [Arguments(200)]
    public async Task GetFailedLogins_WithInvalidHours_ReturnsBadRequest(int hours)
    {
        // Arrange
        var client = _server.CreateAuthenticatedClient(role: "GlobalAdmin");

        // Act
        var response = await client.GetAsync($"/admin/security/failed-logins?hours={hours}");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    // ==================== Active Sessions ====================

    [Test]
    public async Task GetActiveSessions_WithAdminAuth_ReturnsOk()
    {
        // Arrange
        var client = _server.CreateAuthenticatedClient(role: "GlobalAdmin");

        // Act
        var response = await client.GetAsync("/admin/security/active-sessions");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        await Assert.That(json.RootElement.TryGetProperty("sessions", out _)).IsTrue();
        await Assert.That(json.RootElement.TryGetProperty("total", out _)).IsTrue();
    }

    // ==================== Suspicious Activities ====================

    [Test]
    public async Task GetSuspiciousActivities_WithAdminAuth_ReturnsOk()
    {
        // Arrange
        var client = _server.CreateAuthenticatedClient(role: "GlobalAdmin");

        // Act
        var response = await client.GetAsync("/admin/security/suspicious-activities");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        await Assert.That(json.RootElement.TryGetProperty("activities", out _)).IsTrue();
        await Assert.That(json.RootElement.TryGetProperty("total", out _)).IsTrue();
    }

    // ==================== User Data Export ====================

    [Test]
    public async Task ExportUserData_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.PostAsync("/admin/security/user-data-export/some-user-id", null);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetUserDataExport_WithAdminAuth_ReturnsOkWithPendingStatus()
    {
        // Arrange
        var client = _server.CreateAuthenticatedClient(role: "GlobalAdmin");
        var exportId = "test-export-123";

        // Act
        var response = await client.GetAsync($"/admin/security/user-data-export/{exportId}");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        await Assert.That(json.RootElement.GetProperty("exportId").GetString()).IsEqualTo(exportId);
        await Assert.That(json.RootElement.GetProperty("status").GetString()).IsEqualTo("pending");
    }

    public async ValueTask DisposeAsync()
    {
        await _server.DisposeAsync();
    }
}
