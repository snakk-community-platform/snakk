using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Snakk.Web.Tests.Helpers;

namespace Snakk.Web.Tests.Endpoints;

/// <summary>
/// Integration tests for miscellaneous BFF endpoints:
///   GET  /bff/homepage-data
///   POST /bff/markup/preview
///   POST /bff/moderation/reports
///   GET  /bff/moderation/reports/reasons
///   POST /bff/read-states/batch
///   PUT  /bff/spaces/{spaceId}/follow-level
/// </summary>
public class BffMiscTests
{
    // ==================== Homepage Data ====================

    [Test]
    public async Task GetHomepageData_ReturnsAggregatedDataFromAllApis()
    {
        // Arrange
        await using var app = new TestWebApp();

        app.MockApiHandler.SetupJsonResponse("/discussions/recent", new
        {
            items = new[]
            {
                new
                {
                    publicId = "disc-001",
                    title = "Recent Discussion",
                    slug = "recent-discussion",
                    postCount = 3
                }
            },
            offset = 0,
            pageSize = 10,
            hasMoreItems = false
        });

        app.MockApiHandler.SetupJsonResponse("/discussions/top-active-today", new
        {
            items = new[]
            {
                new
                {
                    publicId = "disc-002",
                    title = "Hot Discussion",
                    slug = "hot-discussion",
                    postCountToday = 15,
                    space = new { publicId = "space-001", slug = "general", name = "General" },
                    hub = new { publicId = "hub-001", slug = "main", name = "Main Hub" },
                    author = new { publicId = "user-001", displayName = "Active User" }
                }
            }
        });

        app.MockApiHandler.SetupJsonResponse("/spaces/top-active-today", new
        {
            items = new[]
            {
                new
                {
                    publicId = "space-001",
                    name = "General",
                    slug = "general",
                    postCountToday = 42,
                    hub = new { publicId = "hub-001", slug = "main", name = "Main Hub" }
                }
            }
        });

        app.MockApiHandler.SetupJsonResponse("/api/users/top-contributors-today", new
        {
            items = new[]
            {
                new
                {
                    publicId = "user-001",
                    displayName = "Top Contributor",
                    postCountToday = 20
                }
            }
        });

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.GetAsync("/bff/homepage-data?offset=0&pageSize=10");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        // Verify recentDiscussions
        var recent = body.GetProperty("recentDiscussions");
        var recentItems = recent.GetProperty("items");
        await Assert.That(recentItems.GetArrayLength()).IsEqualTo(1);
        await Assert.That(recentItems[0].GetProperty("title").GetString()).IsEqualTo("Recent Discussion");

        // Verify topActiveDiscussions
        var topDiscussions = body.GetProperty("topActiveDiscussions");
        var topDiscItems = topDiscussions.GetProperty("items");
        await Assert.That(topDiscItems.GetArrayLength()).IsEqualTo(1);
        await Assert.That(topDiscItems[0].GetProperty("title").GetString()).IsEqualTo("Hot Discussion");

        // Verify topActiveSpaces
        var topSpaces = body.GetProperty("topActiveSpaces");
        var topSpaceItems = topSpaces.GetProperty("items");
        await Assert.That(topSpaceItems.GetArrayLength()).IsEqualTo(1);

        // Verify topContributors
        var topContributors = body.GetProperty("topContributors");
        var topContribItems = topContributors.GetProperty("items");
        await Assert.That(topContribItems.GetArrayLength()).IsEqualTo(1);
        await Assert.That(topContribItems[0].GetProperty("displayName").GetString()).IsEqualTo("Top Contributor");
    }

    [Test]
    public async Task GetHomepageData_WhenOneApiFails_ReturnsPartialDataWithNullFallback()
    {
        // Arrange
        await using var app = new TestWebApp();

        app.MockApiHandler.SetupJsonResponse("/discussions/recent", new
        {
            items = new[]
            {
                new { publicId = "disc-001", title = "Recent Discussion", slug = "recent", postCount = 1 }
            },
            offset = 0,
            pageSize = 10,
            hasMoreItems = false
        });

        // top-active-today returns error → GetTopActiveDiscussionsTodayAsync catches and returns null
        app.MockApiHandler.SetupResponse("/discussions/top-active-today", HttpStatusCode.InternalServerError);

        app.MockApiHandler.SetupJsonResponse("/spaces/top-active-today", new
        {
            items = new[]
            {
                new
                {
                    publicId = "space-001",
                    name = "General",
                    slug = "general",
                    postCountToday = 5,
                    hub = new { publicId = "hub-001", slug = "main", name = "Main Hub" }
                }
            }
        });

        app.MockApiHandler.SetupJsonResponse("/api/users/top-contributors-today", new
        {
            items = new[]
            {
                new { publicId = "user-001", displayName = "Contributor", postCountToday = 3 }
            }
        });

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.GetAsync("/bff/homepage-data?offset=0&pageSize=10");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        // recentDiscussions should still be present
        var recent = body.GetProperty("recentDiscussions");
        await Assert.That(recent.GetProperty("items").GetArrayLength()).IsEqualTo(1);

        // topActiveDiscussions should be null (caught exception)
        var topDiscussions = body.GetProperty("topActiveDiscussions");
        await Assert.That(topDiscussions.ValueKind).IsEqualTo(JsonValueKind.Null);

        // topActiveSpaces should still be present
        var topSpaces = body.GetProperty("topActiveSpaces");
        await Assert.That(topSpaces.GetProperty("items").GetArrayLength()).IsEqualTo(1);
    }

    // ==================== Markup Preview ====================

    [Test]
    public async Task PreviewMarkup_ReturnsHtmlFromApi()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiHandler.SetupJsonResponse("/api/markup/preview", new
        {
            html = "<p>This is <strong>bold</strong> text</p>"
        });

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.PostAsJsonAsync("/bff/markup/preview", new { content = "This is **bold** text" });

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(body.GetProperty("html").GetString()).IsEqualTo("<p>This is <strong>bold</strong> text</p>");
    }

    [Test]
    public async Task PreviewMarkup_WhenApiFails_ReturnsEmptyHtml()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiHandler.SetupResponse("/api/markup/preview", HttpStatusCode.InternalServerError);

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.PostAsJsonAsync("/bff/markup/preview", new { content = "some markdown" });

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(body.GetProperty("html").GetString()).IsEqualTo(string.Empty);
    }

    // ==================== Moderation Reports ====================

    [Test]
    public async Task CreateReport_ReturnsOkAndForwardsToApi()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiHandler.SetupJsonResponse("/api/moderation/reports", new
        {
            publicId = "report-001",
            entityType = "Post",
            entityId = "post-123",
            reason = "Spam",
            status = "Pending"
        });

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.PostAsJsonAsync("/bff/moderation/reports", new
        {
            entityType = "Post",
            entityId = "post-123",
            reason = "Spam",
            description = "This is spam"
        });

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        // Verify the API received the request
        var apiCalls = app.MockApiHandler.ReceivedRequests
            .Where(r => r.RequestUri?.PathAndQuery?.Contains("/api/moderation/reports") == true
                        && r.Method == HttpMethod.Post)
            .ToList();
        await Assert.That(apiCalls.Count).IsEqualTo(1);
    }

    // ==================== Report Reasons ====================

    [Test]
    public async Task GetReportReasons_ReturnsReasonsFromApi()
    {
        // Arrange
        await using var app = new TestWebApp();
        // GetReportReasonsAsync returns result?.Items (an IEnumerable), so the mock
        // must wrap items in an object with "items" property for deserialization
        app.MockApiHandler.SetupJsonResponse("/api/moderation/report-reasons", new
        {
            items = new[]
            {
                new { publicId = "reason-001", name = "Spam", description = "Unsolicited content" },
                new { publicId = "reason-002", name = "Harassment", description = "Abusive behavior" },
                new { publicId = "reason-003", name = "Off-topic", description = "Irrelevant content" }
            }
        });

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.GetAsync("/bff/moderation/reports/reasons");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        // The BFF returns result (IEnumerable) directly, so the response is a JSON array
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(body.ValueKind).IsEqualTo(JsonValueKind.Array);
        await Assert.That(body.GetArrayLength()).IsEqualTo(3);
        await Assert.That(body[0].GetProperty("name").GetString()).IsEqualTo("Spam");
    }

    [Test]
    public async Task GetReportReasons_WhenApiFails_ReturnsEmptyArray()
    {
        // Arrange
        await using var app = new TestWebApp();
        // GetReportReasonsAsync catches exceptions and returns null
        app.MockApiHandler.SetupResponse("/api/moderation/report-reasons", HttpStatusCode.InternalServerError);

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.GetAsync("/bff/moderation/reports/reasons");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(body.ValueKind).IsEqualTo(JsonValueKind.Array);
        await Assert.That(body.GetArrayLength()).IsEqualTo(0);
    }

    // ==================== Batch Read States ====================

    [Test]
    public async Task BatchUpdateReadStates_ReturnsOk()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiHandler.SetupResponse("/api/read-states/batch", HttpStatusCode.OK);

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.PostAsJsonAsync("/bff/read-states/batch", new
        {
            updates = new[]
            {
                new { discussionId = "disc-001", postId = "post-010", timestamp = 1709000000000L },
                new { discussionId = "disc-002", postId = "post-020", timestamp = 1709000001000L }
            }
        });

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        // Verify the API received the batch request
        var apiCalls = app.MockApiHandler.ReceivedRequests
            .Where(r => r.RequestUri?.PathAndQuery?.Contains("/api/read-states/batch") == true
                        && r.Method == HttpMethod.Post)
            .ToList();
        await Assert.That(apiCalls.Count).IsEqualTo(1);
    }

    // ==================== Set Space Follow Level ====================

    [Test]
    public async Task SetSpaceFollowLevel_ReturnsUpdatedFollowResult()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiHandler.SetupJsonResponse("/api/spaces/space-001/follow-level", new
        {
            isFollowing = true,
            level = "AllActivity"
        });

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.PutAsync("/bff/spaces/space-001/follow-level?level=AllActivity", null);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(body.GetProperty("isFollowing").GetBoolean()).IsTrue();
        await Assert.That(body.GetProperty("level").GetString()).IsEqualTo("AllActivity");
    }

    [Test]
    public async Task SetSpaceFollowLevel_WhenApiFails_ReturnsBadRequest()
    {
        // Arrange
        await using var app = new TestWebApp();
        // SetSpaceFollowLevelAsync catches exceptions and returns null → BFF returns BadRequest
        app.MockApiHandler.SetupResponse("/api/spaces/space-001/follow-level", HttpStatusCode.InternalServerError);

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.PutAsync("/bff/spaces/space-001/follow-level?level=AllActivity", null);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }
}
