using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Snakk.Web.Tests.Helpers;

namespace Snakk.Web.Tests.Endpoints;

/// <summary>
/// Integration tests for BFF notification endpoints:
///   GET  /bff/notifications
///   GET  /bff/notifications/unread-count
///   POST /bff/notifications/{notificationId}/read
///   POST /bff/notifications/read-all
/// </summary>
public class BffNotificationTests
{
    [Test]
    public async Task GetNotifications_ProxiesToApiAndMapsResponse()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiHandler.SetupJsonResponse("/api/notifications", new
        {
            items = new[]
            {
                new
                {
                    publicId = "notif-001",
                    type = "NewReply",
                    title = "Someone replied to your post",
                    body = "Check it out!",
                    sourcePostId = "post-123",
                    sourceDiscussionId = "disc-456",
                    actorUserId = "user-002",
                    isRead = false,
                    createdAt = "2026-02-25T10:00:00Z"
                }
            },
            offset = 0,
            pageSize = 10,
            hasMoreItems = false
        });

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.GetAsync("/bff/notifications?offset=0&pageSize=10");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items");
        await Assert.That(items.GetArrayLength()).IsEqualTo(1);

        var firstItem = items[0];
        await Assert.That(firstItem.GetProperty("publicId").GetString()).IsEqualTo("notif-001");
        await Assert.That(firstItem.GetProperty("type").GetString()).IsEqualTo("NewReply");
        await Assert.That(firstItem.GetProperty("title").GetString()).IsEqualTo("Someone replied to your post");
        await Assert.That(firstItem.GetProperty("isRead").GetBoolean()).IsFalse();
        await Assert.That(firstItem.GetProperty("sourceDiscussionId").GetString()).IsEqualTo("disc-456");
    }

    [Test]
    public async Task GetNotifications_WhenApiReturnsNull_ReturnsEmptyList()
    {
        // Arrange
        await using var app = new TestWebApp();
        // Return a 404 which causes SnakkApiClient to return null
        app.MockApiHandler.SetupResponse("/api/notifications", HttpStatusCode.NotFound);

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.GetAsync("/bff/notifications?offset=0&pageSize=10");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items");
        await Assert.That(items.GetArrayLength()).IsEqualTo(0);
    }

    [Test]
    public async Task GetUnreadCount_ReturnsCountFromApi()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiHandler.SetupJsonResponse("/api/notifications/unread-count", new
        {
            count = 5
        });

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.GetAsync("/bff/notifications/unread-count");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(body.GetProperty("count").GetInt32()).IsEqualTo(5);
    }

    [Test]
    public async Task GetUnreadCount_WhenApiFails_ReturnsZero()
    {
        // Arrange
        await using var app = new TestWebApp();
        // SnakkApiClient.GetUnreadNotificationCountAsync catches exceptions and returns UnreadCountDto(0)
        app.MockApiHandler.SetupResponse("/api/notifications/unread-count", HttpStatusCode.InternalServerError);

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.GetAsync("/bff/notifications/unread-count");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(body.GetProperty("count").GetInt32()).IsEqualTo(0);
    }

    [Test]
    public async Task MarkNotificationAsRead_ProxiesPostToApi()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiHandler.SetupResponse("/api/notifications/notif-001/read", HttpStatusCode.OK);

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.PostAsync("/bff/notifications/notif-001/read", null);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        // Verify the API was called
        var apiCalls = app.MockApiHandler.ReceivedRequests
            .Where(r => r.RequestUri?.PathAndQuery?.Contains("/api/notifications/notif-001/read") == true)
            .ToList();
        await Assert.That(apiCalls.Count).IsEqualTo(1);
    }

    [Test]
    public async Task MarkAllNotificationsAsRead_ProxiesPostToApi()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiHandler.SetupResponse("/api/notifications/read-all", HttpStatusCode.OK);

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.PostAsync("/bff/notifications/read-all", null);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var apiCalls = app.MockApiHandler.ReceivedRequests
            .Where(r => r.RequestUri?.PathAndQuery?.Contains("/api/notifications/read-all") == true)
            .ToList();
        await Assert.That(apiCalls.Count).IsEqualTo(1);
    }
}
