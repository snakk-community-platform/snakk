using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using NSubstitute;
using Snakk.Protos.Community;
using Snakk.Protos.Discussion;
using Snakk.Protos.Hub;
using Snakk.Protos.Space;
using Snakk.Protos.Statistics;
using Snakk.Web.Tests.Helpers;

namespace Snakk.Web.Tests.Endpoints;

/// <summary>
/// Integration tests for BFF entity stats popup endpoints:
///   GET /bff/hubs/{publicId}/stats
///   GET /bff/spaces/{publicId}/stats
///   GET /bff/communities/{publicId}/stats
///   GET /bff/users/{publicId}/stats-popup
///   GET /bff/discussions/{publicId}/stats
///   GET /bff/users/{userId}/stats
///   GET /bff/users/{userId}/activity-history
/// </summary>
public class BffEntityStatsTests
{
    // ==================== Hub Stats ====================

    [Test]
    public async Task GetHubStats_ReturnsHubStatsResponse()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiClient
            .GetHubStatsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new HubStats
            {
                PublicId = "hub-001",
                Name = "Test Hub",
                Description = "A test hub for unit tests",
                AvatarUrl = "/avatars/hub-001.png",
                SpaceCount = 5,
                DiscussionCount = 42,
                ReplyCount = 128
            });

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.GetAsync("/bff/hubs/hub-001/stats");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(body.GetProperty("publicId").GetString()).IsEqualTo("hub-001");
        await Assert.That(body.GetProperty("name").GetString()).IsEqualTo("Test Hub");
        await Assert.That(body.GetProperty("description").GetString()).IsEqualTo("A test hub for unit tests");
        await Assert.That(body.GetProperty("avatarUrl").GetString()).IsEqualTo("/avatars/hub-001.png");
        await Assert.That(body.GetProperty("spaceCount").GetInt32()).IsEqualTo(5);
        await Assert.That(body.GetProperty("discussionCount").GetInt32()).IsEqualTo(42);
        await Assert.That(body.GetProperty("replyCount").GetInt32()).IsEqualTo(128);
    }

    [Test]
    public async Task GetHubStats_WhenApiReturnsNull_ReturnsNotFound()
    {
        // Arrange
        await using var app = new TestWebApp();
        // SnakkApiClient.GetHubStatsAsync catches RpcException and returns null
        app.MockApiClient
            .GetHubStatsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((HubStats?)null);

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.GetAsync("/bff/hubs/hub-999/stats");

        // Assert — BFF returns NotFound when apiClient returns null
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    // ==================== Space Stats ====================

    [Test]
    public async Task GetSpaceStats_ReturnsSpaceStatsResponse()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiClient
            .GetSpaceStatsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new SpaceStats
            {
                PublicId = "space-001",
                Name = "Test Space",
                Description = "A test space",
                AvatarUrl = "/avatars/space-001.png",
                DiscussionCount = 15,
                ReplyCount = 87,
                FollowerCount = 23
            });

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.GetAsync("/bff/spaces/space-001/stats");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(body.GetProperty("publicId").GetString()).IsEqualTo("space-001");
        await Assert.That(body.GetProperty("name").GetString()).IsEqualTo("Test Space");
        await Assert.That(body.GetProperty("description").GetString()).IsEqualTo("A test space");
        await Assert.That(body.GetProperty("avatarUrl").GetString()).IsEqualTo("/avatars/space-001.png");
        await Assert.That(body.GetProperty("discussionCount").GetInt32()).IsEqualTo(15);
        await Assert.That(body.GetProperty("replyCount").GetInt32()).IsEqualTo(87);
        await Assert.That(body.GetProperty("followerCount").GetInt32()).IsEqualTo(23);
    }

    [Test]
    public async Task GetSpaceStats_WhenApiReturnsNull_ReturnsNotFound()
    {
        // Arrange
        await using var app = new TestWebApp();
        // SnakkApiClient.GetSpaceStatsAsync catches RpcException and returns null
        app.MockApiClient
            .GetSpaceStatsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((SpaceStats?)null);

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.GetAsync("/bff/spaces/space-999/stats");

        // Assert — BFF returns NotFound when apiClient returns null
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    // ==================== Community Stats ====================

    [Test]
    public async Task GetCommunityStats_ReturnsCommunityStatsResponse()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiClient
            .GetCommunityStatsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new CommunityStats
            {
                PublicId = "comm-001",
                Name = "Test Community",
                Description = "A test community",
                AvatarUrl = "/avatars/comm-001.png",
                HubCount = 3,
                SpaceCount = 12,
                DiscussionCount = 150,
                ReplyCount = 430
            });

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.GetAsync("/bff/communities/comm-001/stats");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(body.GetProperty("publicId").GetString()).IsEqualTo("comm-001");
        await Assert.That(body.GetProperty("name").GetString()).IsEqualTo("Test Community");
        await Assert.That(body.GetProperty("description").GetString()).IsEqualTo("A test community");
        await Assert.That(body.GetProperty("avatarUrl").GetString()).IsEqualTo("/avatars/comm-001.png");
        await Assert.That(body.GetProperty("hubCount").GetInt32()).IsEqualTo(3);
        await Assert.That(body.GetProperty("spaceCount").GetInt32()).IsEqualTo(12);
        await Assert.That(body.GetProperty("discussionCount").GetInt32()).IsEqualTo(150);
        await Assert.That(body.GetProperty("replyCount").GetInt32()).IsEqualTo(430);
    }

    [Test]
    public async Task GetCommunityStats_WhenApiReturnsNull_ReturnsNotFound()
    {
        // Arrange
        await using var app = new TestWebApp();
        // SnakkApiClient.GetCommunityStatsAsync catches RpcException and returns null
        app.MockApiClient
            .GetCommunityStatsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((CommunityStats?)null);

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.GetAsync("/bff/communities/comm-999/stats");

        // Assert — BFF returns NotFound when apiClient returns null
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    // ==================== User Stats Popup ====================

    [Test]
    public async Task GetUserStatsPopup_ReturnsUserStatsResponse()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiClient
            .GetUserStatsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new UserStats
            {
                PublicId = "user-001",
                DisplayName = "Test User",
                AvatarUrl = "/avatars/user-001.png",
                DiscussionCount = 10,
                ReplyCount = 55,
                FollowerCount = 8,
                FollowingCount = 12
            });

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.GetAsync("/bff/users/user-001/stats-popup");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(body.GetProperty("publicId").GetString()).IsEqualTo("user-001");
        await Assert.That(body.GetProperty("displayName").GetString()).IsEqualTo("Test User");
        await Assert.That(body.GetProperty("avatarUrl").GetString()).IsEqualTo("/avatars/user-001.png");
        await Assert.That(body.GetProperty("discussionCount").GetInt32()).IsEqualTo(10);
        await Assert.That(body.GetProperty("replyCount").GetInt32()).IsEqualTo(55);
        await Assert.That(body.GetProperty("followerCount").GetInt32()).IsEqualTo(8);
        await Assert.That(body.GetProperty("followingCount").GetInt32()).IsEqualTo(12);
    }

    [Test]
    public async Task GetUserStatsPopup_WhenApiReturnsNull_ReturnsNotFound()
    {
        // Arrange
        await using var app = new TestWebApp();
        // SnakkApiClient.GetUserStatsAsync catches RpcException and returns null
        app.MockApiClient
            .GetUserStatsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((UserStats?)null);

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.GetAsync("/bff/users/user-999/stats-popup");

        // Assert — BFF returns NotFound when apiClient returns null
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    // ==================== Discussion Stats ====================

    [Test]
    public async Task GetDiscussionStats_ReturnsDiscussionStatsResponse()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiClient
            .GetDiscussionStatsForPopupAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new DiscussionStats
            {
                PublicId = "disc-001",
                Title = "Test Discussion",
                ReplyCount = 25,
                FollowerCount = 10
            });

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.GetAsync("/bff/discussions/disc-001/stats");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(body.GetProperty("replyCount").GetInt32()).IsEqualTo(25);
        await Assert.That(body.GetProperty("followerCount").GetInt32()).IsEqualTo(10);
    }

    [Test]
    public async Task GetDiscussionStats_WhenApiReturnsNull_ReturnsNotFound()
    {
        // Arrange
        await using var app = new TestWebApp();
        // SnakkApiClient.GetDiscussionStatsForPopupAsync catches RpcException and returns null
        app.MockApiClient
            .GetDiscussionStatsForPopupAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((DiscussionStats?)null);

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.GetAsync("/bff/discussions/disc-999/stats");

        // Assert — BFF returns NotFound when result is null
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    // ==================== User Stats (regular) ====================

    [Test]
    public async Task GetUserStats_ReturnsUserStatsResponse()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiClient
            .GetUserStatsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new UserStats
            {
                PublicId = "user-002",
                DisplayName = "Another User",
                AvatarUrl = "/avatars/user-002.png",
                DiscussionCount = 3,
                ReplyCount = 19,
                FollowerCount = 2,
                FollowingCount = 7
            });

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.GetAsync("/bff/users/user-002/stats");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(body.GetProperty("publicId").GetString()).IsEqualTo("user-002");
        await Assert.That(body.GetProperty("displayName").GetString()).IsEqualTo("Another User");
        await Assert.That(body.GetProperty("avatarUrl").GetString()).IsEqualTo("/avatars/user-002.png");
        await Assert.That(body.GetProperty("discussionCount").GetInt32()).IsEqualTo(3);
        await Assert.That(body.GetProperty("replyCount").GetInt32()).IsEqualTo(19);
        await Assert.That(body.GetProperty("followerCount").GetInt32()).IsEqualTo(2);
        await Assert.That(body.GetProperty("followingCount").GetInt32()).IsEqualTo(7);
    }

    [Test]
    public async Task GetUserStats_WhenApiReturnsNull_ReturnsNotFound()
    {
        // Arrange
        await using var app = new TestWebApp();
        // SnakkApiClient.GetUserStatsAsync catches RpcException and returns null
        app.MockApiClient
            .GetUserStatsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((UserStats?)null);

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.GetAsync("/bff/users/user-999/stats");

        // Assert — BFF returns NotFound when apiClient returns null
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    // ==================== User Activity History ====================

    [Test]
    public async Task GetUserActivityHistory_ReturnsActivityResponse()
    {
        // Arrange
        await using var app = new TestWebApp();
        var activityHistory = new UserActivityHistory { Days = 30 };
        activityHistory.Data.Add(new ActivityDay { Date = "2026-02-25", Posts = 3, Discussions = 1, Total = 4 });
        activityHistory.Data.Add(new ActivityDay { Date = "2026-02-26", Posts = 5, Discussions = 2, Total = 7 });
        activityHistory.Data.Add(new ActivityDay { Date = "2026-02-27", Posts = 0, Discussions = 0, Total = 0 });

        app.MockApiClient
            .GetUserActivityHistoryAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(activityHistory);

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.GetAsync("/bff/users/user-001/activity-history?days=30");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var activities = body.GetProperty("activities");
        await Assert.That(activities.GetArrayLength()).IsEqualTo(3);

        var first = activities[0];
        await Assert.That(first.GetProperty("date").GetString()).IsEqualTo("2026-02-25");
        await Assert.That(first.GetProperty("postCount").GetInt32()).IsEqualTo(3);
        await Assert.That(first.GetProperty("discussionCount").GetInt32()).IsEqualTo(1);
    }

    [Test]
    public async Task GetUserActivityHistory_WhenApiReturnsNull_ReturnsNotFound()
    {
        // Arrange
        await using var app = new TestWebApp();
        // SnakkApiClient.GetUserActivityHistoryAsync catches RpcException and returns null
        app.MockApiClient
            .GetUserActivityHistoryAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((UserActivityHistory?)null);

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.GetAsync("/bff/users/user-999/activity-history?days=30");

        // Assert — BFF returns NotFound when apiClient returns null
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }
}
