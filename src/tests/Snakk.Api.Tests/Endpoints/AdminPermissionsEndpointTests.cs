using System.Net;
using System.Net.Http.Json;
using Snakk.Api.Tests.Helpers;

namespace Snakk.Api.Tests.Endpoints;

public class AdminPermissionsEndpointTests : IAsyncDisposable
{
    private readonly TestWebServer _server = new();

    // ==================== GET /admin/permissions ====================

    [Test]
    public async Task GetAllPermissions_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync("/admin/permissions");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetAllPermissions_WithAuth_ReturnsOk()
    {
        // Arrange
        var client = _server.CreateAuthenticatedClient(role: "Admin");

        // Act
        var response = await client.GetAsync("/admin/permissions");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    // ==================== GET /admin/permissions/roles/{roleId} ====================

    [Test]
    public async Task GetRolePermissions_WithAuth_ReturnsOk()
    {
        // Arrange
        var client = _server.CreateAuthenticatedClient(role: "Admin");
        var roleId = 1;

        // Act
        var response = await client.GetAsync($"/admin/permissions/roles/{roleId}");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    // ==================== POST /admin/permissions/roles/{roleId} ====================

    [Test]
    public async Task AssignPermission_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var client = _server.CreateClient();
        var roleId = 1;
        var body = new { permissionId = 1 };

        // Act
        var response = await client.PostAsJsonAsync($"/admin/permissions/roles/{roleId}", body);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task AssignPermission_WithAuth_ReturnsOkOrError()
    {
        // Arrange
        var client = _server.CreateAuthenticatedClient(role: "Admin");
        var roleId = 1;
        var body = new { permissionId = 1 };

        // Act
        var response = await client.PostAsJsonAsync($"/admin/permissions/roles/{roleId}", body);

        // Assert — endpoint is reachable (not 401/403); may return OK or a domain error
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Forbidden);
    }

    // ==================== DELETE /admin/permissions/roles/{roleId}/{permissionId} ====================

    [Test]
    public async Task RevokePermission_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var client = _server.CreateClient();
        var roleId = 1;
        var permissionId = 1;

        // Act
        var response = await client.DeleteAsync($"/admin/permissions/roles/{roleId}/{permissionId}");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    // ==================== GET /admin/temporary-roles ====================

    [Test]
    public async Task GetActiveTemporaryRoles_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync("/admin/temporary-roles");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetActiveTemporaryRoles_WithAuth_ReturnsOk()
    {
        // Arrange
        var client = _server.CreateAuthenticatedClient(role: "Admin");

        // Act
        var response = await client.GetAsync("/admin/temporary-roles");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    // ==================== POST /admin/temporary-roles ====================

    [Test]
    public async Task GrantTemporaryRole_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var client = _server.CreateClient();
        var body = new
        {
            userId = "some-user-id",
            roleType = "SpaceMod",
            scope = "Space",
            scopeId = "some-scope-id",
            expiresAt = DateTime.UtcNow.AddDays(7).ToString("o"),
            reason = "Testing temporary elevation"
        };

        // Act
        var response = await client.PostAsJsonAsync("/admin/temporary-roles", body);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    // ==================== DELETE /admin/temporary-roles/{elevationId} ====================

    [Test]
    public async Task RevokeTemporaryRole_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var client = _server.CreateClient();
        var elevationId = "some-elevation-id";

        // Act
        var request = new HttpRequestMessage(HttpMethod.Delete, $"/admin/temporary-roles/{elevationId}")
        {
            Content = JsonContent.Create(new { reason = "No longer needed" })
        };
        var response = await client.SendAsync(request);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    public async ValueTask DisposeAsync()
    {
        await _server.DisposeAsync();
    }
}
