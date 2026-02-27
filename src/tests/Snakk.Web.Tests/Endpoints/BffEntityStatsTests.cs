using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
        app.MockApiHandler.SetupJsonResponse("/api/hubs/hub-001/stats", new
        {
            publicId = "hub-001",
            name = "Test Hub",
            description = "A test hub for unit tests",
            avatarUrl = "/avatars/hub-001.png",
            spaceCount = 5,
            discussionCount = 42,
            replyCount = 128
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
    public async Task GetHubStats_WhenApiFails_ReturnsNotFound()
    {
        // Arrange
        await using var app = new TestWebApp();
        // SnakkApiClient.GetHubStatsAsync has try-catch, returns null on failure
        app.MockApiHandler.SetupResponse("/api/hubs/hub-999/stats", HttpStatusCode.InternalServerError);

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.GetAsync("/bff/hubs/hub-999/stats");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    // ==================== Space Stats ====================

    [Test]
    public async Task GetSpaceStats_ReturnsSpaceStatsResponse()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiHandler.SetupJsonResponse("/api/spaces/space-001/stats", new
        {
            publicId = "space-001",
            name = "Test Space",
            description = "A test space",
            avatarUrl = "/avatars/space-001.png",
            discussionCount = 15,
            replyCount = 87,
            followerCount = 23
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
    public async Task GetSpaceStats_WhenApiFails_ReturnsNotFound()
    {
        // Arrange
        await using var app = new TestWebApp();
        // SnakkApiClient.GetSpaceStatsAsync has try-catch, returns null on failure
        app.MockApiHandler.SetupResponse("/api/spaces/space-999/stats", HttpStatusCode.InternalServerError);

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.GetAsync("/bff/spaces/space-999/stats");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    // ==================== Community Stats ====================

    [Test]
    public async Task GetCommunityStats_ReturnsCommunityStatsResponse()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiHandler.SetupJsonResponse("/api/communities/comm-001/stats", new
        {
            publicId = "comm-001",
            name = "Test Community",
            description = "A test community",
            avatarUrl = "/avatars/comm-001.png",
            hubCount = 3,
            spaceCount = 12,
            discussionCount = 150,
            replyCount = 430
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
    public async Task GetCommunityStats_WhenApiFails_ReturnsNotFound()
    {
        // Arrange
        await using var app = new TestWebApp();
        // SnakkApiClient.GetCommunityStatsAsync has try-catch, returns null on failure
        app.MockApiHandler.SetupResponse("/api/communities/comm-999/stats", HttpStatusCode.InternalServerError);

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.GetAsync("/bff/communities/comm-999/stats");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    // ==================== User Stats Popup ====================

    [Test]
    public async Task GetUserStatsPopup_ReturnsUserStatsResponse()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiHandler.SetupJsonResponse("/api/users/user-001/stats", new
        {
            publicId = "user-001",
            displayName = "Test User",
            avatarUrl = "/avatars/user-001.png",
            discussionCount = 10,
            replyCount = 55,
            followerCount = 8,
            followingCount = 12
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
    public async Task GetUserStatsPopup_WhenApiReturnsNotFound_ReturnsNotFound()
    {
        // Arrange
        await using var app = new TestWebApp();
        // SnakkApiClient.GetUserStatsAsync has no try-catch; GetFromJsonAsync throws on non-success
        app.MockApiHandler.SetupResponse("/api/users/user-999/stats", HttpStatusCode.NotFound);

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.GetAsync("/bff/users/user-999/stats-popup");

        // Assert — the unhandled exception propagates as 500
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.InternalServerError);
    }

    // ==================== Discussion Stats ====================

    [Test]
    public async Task GetDiscussionStats_ReturnsDiscussionStatsResponse()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiHandler.SetupJsonResponse("/api/discussions/disc-001/stats", new
        {
            postCount = 25,
            views = 340
        });

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.GetAsync("/bff/discussions/disc-001/stats");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(body.GetProperty("postCount").GetInt32()).IsEqualTo(25);
        await Assert.That(body.GetProperty("views").GetInt32()).IsEqualTo(340);
    }

    [Test]
    public async Task GetDiscussionStats_WhenApiReturnsNotFound_ReturnsError()
    {
        // Arrange
        await using var app = new TestWebApp();
        // SnakkApiClient.GetDiscussionStatsForPopupAsync has no try-catch; throws on non-success
        app.MockApiHandler.SetupResponse("/api/discussions/disc-999/stats", HttpStatusCode.NotFound);

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.GetAsync("/bff/discussions/disc-999/stats");

        // Assert — the unhandled exception propagates as 500
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.InternalServerError);
    }

    // ==================== User Stats (regular) ====================

    [Test]
    public async Task GetUserStats_ReturnsUserStatsResponse()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiHandler.SetupJsonResponse("/api/users/user-002/stats", new
        {
            publicId = "user-002",
            displayName = "Another User",
            avatarUrl = "/avatars/user-002.png",
            discussionCount = 3,
            replyCount = 19,
            followerCount = 2,
            followingCount = 7
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
    public async Task GetUserStats_WhenApiReturnsNotFound_ReturnsError()
    {
        // Arrange
        await using var app = new TestWebApp();
        // SnakkApiClient.GetUserStatsAsync has no try-catch; GetFromJsonAsync throws on non-success
        app.MockApiHandler.SetupResponse("/api/users/user-999/stats", HttpStatusCode.NotFound);

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.GetAsync("/bff/users/user-999/stats");

        // Assert — the unhandled exception propagates as 500
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.InternalServerError);
    }

    // ==================== User Activity History ====================

    [Test]
    public async Task GetUserActivityHistory_ReturnsActivityResponse()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiHandler.SetupJsonResponse("/api/users/user-001/activity-history", new
        {
            activities = new[]
            {
                new { date = "2026-02-25", postCount = 3, discussionCount = 1 },
                new { date = "2026-02-26", postCount = 5, discussionCount = 2 },
                new { date = "2026-02-27", postCount = 0, discussionCount = 0 }
            }
        });

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
    public async Task GetUserActivityHistory_WhenApiReturnsNotFound_ReturnsError()
    {
        // Arrange
        await using var app = new TestWebApp();
        // SnakkApiClient.GetUserActivityHistoryAsync has no try-catch; throws on non-success
        app.MockApiHandler.SetupResponse("/api/users/user-999/activity-history", HttpStatusCode.NotFound);

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.GetAsync("/bff/users/user-999/activity-history?days=30");

        // Assert — the unhandled exception propagates as 500
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.InternalServerError);
    }
}
