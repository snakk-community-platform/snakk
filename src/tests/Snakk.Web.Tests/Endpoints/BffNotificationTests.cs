using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Google.Protobuf.WellKnownTypes;
using NSubstitute;
using Snakk.Protos.Notification;
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

        var notificationList = new PagedNotificationList
        {
            Offset = 0,
            PageSize = 10,
            HasMoreItems = false
        };
        notificationList.Items.Add(new NotificationInfo
        {
            PublicId = "notif-001",
            Type = "NewReply",
            Message = "Someone replied to your post",
            IsRead = false,
            CreatedAt = Timestamp.FromDateTime(new DateTime(2026, 2, 25, 10, 0, 0, DateTimeKind.Utc)),
            TargetPublicId = "disc-456"
        });

        app.MockApiClient
            .GetNotificationsAsync(Arg.Any<int>(), Arg.Any<int>())
            .Returns(notificationList);

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
        app.MockApiClient
            .GetNotificationsAsync(Arg.Any<int>(), Arg.Any<int>())
            .Returns((PagedNotificationList?)null);

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
        app.MockApiClient
            .GetUnreadNotificationCountAsync()
            .Returns(new UnreadCountResponse { Count = 5 });

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
        // SnakkApiClient.GetUnreadNotificationCountAsync returns UnreadCountResponse(0) on failure
        app.MockApiClient
            .GetUnreadNotificationCountAsync()
            .Returns((UnreadCountResponse?)null);

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
        app.MockApiClient
            .MarkNotificationAsReadAsync(Arg.Any<string>())
            .Returns(Task.CompletedTask);

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.PostAsync("/bff/notifications/notif-001/read", null);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        // Verify the API client was called with the correct notification ID
        await app.MockApiClient.Received(1).MarkNotificationAsReadAsync("notif-001");
    }

    [Test]
    public async Task MarkAllNotificationsAsRead_ProxiesPostToApi()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiClient
            .MarkAllNotificationsAsReadAsync()
            .Returns(Task.CompletedTask);

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.PostAsync("/bff/notifications/read-all", null);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        // Verify the API client was called
        await app.MockApiClient.Received(1).MarkAllNotificationsAsReadAsync();
    }
}
