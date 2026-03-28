using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Snakk.Api.Tests.Helpers;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;

namespace Snakk.Api.Tests.Endpoints;

public class AdminWebhooksEndpointTests : IAsyncDisposable
{
    private readonly TestWebServer _server = new();
    private const string BaseUrl = "/admin/webhooks";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // ==================== Helper Methods ====================

    private bool _defaultUserSeeded;

    /// <summary>
    /// Seeds the default test user ("test-user-id") into the in-memory database.
    /// The WebhookDatabaseEntity.CreatedBy FK references UserDatabaseEntity.PublicId,
    /// so the user must exist before a webhook can be created and re-read.
    /// </summary>
    private async Task EnsureDefaultTestUserSeededAsync()
    {
        if (_defaultUserSeeded) return;

        using var scope = _server.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SnakkDbContext>();

        db.Users.Add(new UserDatabaseEntity
        {
            PublicId = "test-user-id",
            DisplayName = "Test User",
            Email = "test@example.com",
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
        _defaultUserSeeded = true;
    }

    private static StringContent CreateJsonContent(object payload)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    private static object CreateValidWebhookPayload(string? name = null, string? url = null)
    {
        return new
        {
            name = name ?? "Test Webhook",
            url = url ?? "https://93.184.215.14/webhook",
            description = "A test webhook",
            eventTypes = new[] { "post.created" },
            secret = "test-secret-123",
            maxRetries = 3,
            timeoutSeconds = 30,
            isActive = true
        };
    }

    /// <summary>
    /// Creates a webhook via the API and returns its ID parsed from the response.
    /// </summary>
    private async Task<Guid> CreateWebhookAndGetId(HttpClient client, string? name = null)
    {
        await EnsureDefaultTestUserSeededAsync();
        var payload = CreateValidWebhookPayload(name: name);
        var response = await client.PostAsync(BaseUrl, CreateJsonContent(payload));
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        return json.RootElement.GetProperty("id").GetGuid();
    }

    // ==================== GET /api/admin/webhooks ====================

    [Test]
    public async Task GetAllWebhooks_Unauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync(BaseUrl);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetAllWebhooks_Authenticated_ReturnsOk()
    {
        // Arrange
        var client = _server.CreateAuthenticatedClient(role: "Admin");

        // Act
        var response = await client.GetAsync(BaseUrl);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        await Assert.That(json.RootElement.ValueKind).IsEqualTo(JsonValueKind.Array);
    }

    // ==================== POST /api/admin/webhooks ====================

    [Test]
    public async Task CreateWebhook_Unauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var client = _server.CreateClient();
        var payload = CreateValidWebhookPayload();

        // Act
        var response = await client.PostAsync(BaseUrl, CreateJsonContent(payload));

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task CreateWebhook_ValidRequest_ReturnsCreated()
    {
        // Arrange
        await EnsureDefaultTestUserSeededAsync();
        var client = _server.CreateAuthenticatedClient(role: "Admin");
        var payload = CreateValidWebhookPayload(name: "My New Webhook");

        // Act
        var response = await client.PostAsync(BaseUrl, CreateJsonContent(payload));

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Created);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        await Assert.That(json.RootElement.GetProperty("name").GetString()).IsEqualTo("My New Webhook");
        await Assert.That(json.RootElement.GetProperty("url").GetString()).IsEqualTo("https://93.184.215.14/webhook");
        await Assert.That(json.RootElement.GetProperty("isActive").GetBoolean()).IsTrue();
        await Assert.That(json.RootElement.TryGetProperty("id", out _)).IsTrue();
    }

    // ==================== GET /api/admin/webhooks/{webhookId} ====================

    [Test]
    public async Task GetWebhookById_NonExistentId_ReturnsNotFound()
    {
        // Arrange
        var client = _server.CreateAuthenticatedClient(role: "Admin");
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await client.GetAsync($"{BaseUrl}/{nonExistentId}");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    // ==================== PUT /api/admin/webhooks/{webhookId} ====================

    [Test]
    public async Task UpdateWebhook_Unauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var client = _server.CreateClient();
        var updatePayload = new { name = "Updated Name" };

        // Act
        var response = await client.PutAsync($"{BaseUrl}/{Guid.NewGuid()}", CreateJsonContent(updatePayload));

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task UpdateWebhook_NonExistentId_ReturnsNotFound()
    {
        // Arrange
        var client = _server.CreateAuthenticatedClient(role: "Admin");
        var nonExistentId = Guid.NewGuid();
        var updatePayload = new { name = "Updated Name" };

        // Act
        var response = await client.PutAsync($"{BaseUrl}/{nonExistentId}", CreateJsonContent(updatePayload));

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    // ==================== DELETE /api/admin/webhooks/{webhookId} ====================

    [Test]
    public async Task DeleteWebhook_NonExistentId_ReturnsNotFound()
    {
        // Arrange
        var client = _server.CreateAuthenticatedClient(role: "Admin");
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await client.DeleteAsync($"{BaseUrl}/{nonExistentId}");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    // ==================== POST /api/admin/webhooks/{webhookId}/test ====================

    [Test]
    public async Task TestWebhook_NonExistentId_ReturnsNotFound()
    {
        // Arrange
        var client = _server.CreateAuthenticatedClient(role: "Admin");
        var nonExistentId = Guid.NewGuid();
        var testPayload = new { eventType = "post.created" };

        // Act
        var response = await client.PostAsync($"{BaseUrl}/{nonExistentId}/test", CreateJsonContent(testPayload));

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    // ==================== GET /api/admin/webhooks/{webhookId}/logs ====================

    [Test]
    public async Task GetWebhookLogs_Authenticated_ReturnsOk()
    {
        // Arrange
        var client = _server.CreateAuthenticatedClient(role: "Admin");
        var webhookId = await CreateWebhookAndGetId(client);

        // Act
        var response = await client.GetAsync($"{BaseUrl}/{webhookId}/logs");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        await Assert.That(json.RootElement.ValueKind).IsEqualTo(JsonValueKind.Array);
    }

    // ==================== GET /api/admin/webhooks/events/available ====================

    [Test]
    public async Task GetAvailableEventTypes_Unauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync($"{BaseUrl}/events/available");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetAvailableEventTypes_Authenticated_ReturnsList()
    {
        // Arrange
        var client = _server.CreateAuthenticatedClient(role: "Admin");

        // Act
        var response = await client.GetAsync($"{BaseUrl}/events/available");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        await Assert.That(json.RootElement.ValueKind).IsEqualTo(JsonValueKind.Array);
    }

    // ==================== Integration Flows ====================

    [Test]
    public async Task CreateThenGetWebhook_ReturnsConsistentData()
    {
        // Arrange
        var client = _server.CreateAuthenticatedClient(role: "Admin");
        var webhookId = await CreateWebhookAndGetId(client, name: "Consistency Test Webhook");

        // Act
        var response = await client.GetAsync($"{BaseUrl}/{webhookId}");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        await Assert.That(json.RootElement.GetProperty("id").GetGuid()).IsEqualTo(webhookId);
        await Assert.That(json.RootElement.GetProperty("name").GetString()).IsEqualTo("Consistency Test Webhook");
        await Assert.That(json.RootElement.GetProperty("url").GetString()).IsEqualTo("https://93.184.215.14/webhook");
        await Assert.That(json.RootElement.GetProperty("isActive").GetBoolean()).IsTrue();
    }

    [Test]
    public async Task CreateThenUpdateWebhook_UpdatesSuccessfully()
    {
        // Arrange
        var client = _server.CreateAuthenticatedClient(role: "Admin");
        var webhookId = await CreateWebhookAndGetId(client, name: "Original Name");
        var updatePayload = new
        {
            name = "Updated Webhook Name",
            description = "Updated description",
            isActive = false
        };

        // Act
        var response = await client.PutAsync($"{BaseUrl}/{webhookId}", CreateJsonContent(updatePayload));

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        await Assert.That(json.RootElement.GetProperty("name").GetString()).IsEqualTo("Updated Webhook Name");
        await Assert.That(json.RootElement.GetProperty("description").GetString()).IsEqualTo("Updated description");
        await Assert.That(json.RootElement.GetProperty("isActive").GetBoolean()).IsFalse();
    }

    [Test]
    public async Task CreateThenDeleteWebhook_DeleteReturnsNoContent()
    {
        // Arrange
        var client = _server.CreateAuthenticatedClient(role: "Admin");
        var webhookId = await CreateWebhookAndGetId(client);

        // Act
        var response = await client.DeleteAsync($"{BaseUrl}/{webhookId}");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
    }

    [Test]
    public async Task DeleteWebhook_AfterDelete_GetReturnsNotFound()
    {
        // Arrange
        var client = _server.CreateAuthenticatedClient(role: "Admin");
        var webhookId = await CreateWebhookAndGetId(client);

        // Delete the webhook first
        var deleteResponse = await client.DeleteAsync($"{BaseUrl}/{webhookId}");
        await Assert.That(deleteResponse.StatusCode).IsEqualTo(HttpStatusCode.NoContent);

        // Act - try to get the deleted webhook
        var getResponse = await client.GetAsync($"{BaseUrl}/{webhookId}");

        // Assert
        await Assert.That(getResponse.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    public async ValueTask DisposeAsync()
    {
        await _server.DisposeAsync();
    }
}
