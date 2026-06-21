using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using NSubstitute;
using Snakk.Protos;
using Snakk.Protos.Discussion;
using Snakk.Protos.Follow;
using Snakk.Protos.Statistics;
using Snakk.Web.Models;
using Snakk.Web.Services;
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

        var recentDiscussions = new PagedRecentDiscussionList
        {
            Offset = 0,
            PageSize = 10,
            HasMoreItems = false
        };
        recentDiscussions.Items.Add(new RecentDiscussionInfo
        {
            PublicId = "disc-001",
            Title = "Recent Discussion",
            Slug = "recent-discussion",
            PostCount = 3
        });

        var topActiveDiscussions = new TopActiveDiscussionsList();
        topActiveDiscussions.Items.Add(new TopActiveDiscussionInfo
        {
            PublicId = "disc-002",
            Title = "Hot Discussion",
            Slug = "hot-discussion",
            PostCountToday = 15,
            Space = new EntityRef { PublicId = "space-001", Slug = "general", Name = "General" },
            Hub = new EntityRef { PublicId = "hub-001", Slug = "main", Name = "Main Hub" },
            Author = new AuthorRef { PublicId = "user-001", DisplayName = "Active User" }
        });

        var topActiveSpaces = new TopActiveSpacesList();
        topActiveSpaces.Items.Add(new TopActiveSpaceInfo
        {
            PublicId = "space-001",
            Name = "General",
            Slug = "general",
            PostCountToday = 42,
            Hub = new EntityRef { PublicId = "hub-001", Slug = "main", Name = "Main Hub" }
        });

        var topContributors = new TopContributorsList();
        topContributors.Items.Add(new TopContributorInfo
        {
            PublicId = "user-001",
            DisplayName = "Top Contributor",
            PostCountToday = 20
        });

        app.MockApiClient
            .GetRecentDiscussionsAsync(0, 10, null, Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<IReadOnlyList<string>?>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(recentDiscussions);

        app.MockApiClient
            .GetTopActiveDiscussionsAsync(null, Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(topActiveDiscussions);

        app.MockApiClient
            .GetTopActiveSpacesAsync(null, Arg.Any<CancellationToken>())
            .Returns(topActiveSpaces);

        app.MockApiClient
            .GetTopContributorsAsync(null, Arg.Any<CancellationToken>())
            .Returns(topContributors);

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
        var topContrib = body.GetProperty("topContributors");
        var topContribItems = topContrib.GetProperty("items");
        await Assert.That(topContribItems.GetArrayLength()).IsEqualTo(1);
        await Assert.That(topContribItems[0].GetProperty("displayName").GetString()).IsEqualTo("Top Contributor");
    }

    [Test]
    public async Task GetHomepageData_WhenOneApiFails_ReturnsPartialDataWithNullFallback()
    {
        // Arrange
        await using var app = new TestWebApp();

        var recentDiscussions = new PagedRecentDiscussionList
        {
            Offset = 0,
            PageSize = 10,
            HasMoreItems = false
        };
        recentDiscussions.Items.Add(new RecentDiscussionInfo
        {
            PublicId = "disc-001",
            Title = "Recent Discussion",
            Slug = "recent",
            PostCount = 1
        });

        var topActiveSpaces = new TopActiveSpacesList();
        topActiveSpaces.Items.Add(new TopActiveSpaceInfo
        {
            PublicId = "space-001",
            Name = "General",
            Slug = "general",
            PostCountToday = 5,
            Hub = new EntityRef { PublicId = "hub-001", Slug = "main", Name = "Main Hub" }
        });

        var topContributors = new TopContributorsList();
        topContributors.Items.Add(new TopContributorInfo
        {
            PublicId = "user-001",
            DisplayName = "Contributor",
            PostCountToday = 3
        });

        app.MockApiClient
            .GetRecentDiscussionsAsync(0, 10, null, Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<IReadOnlyList<string>?>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(recentDiscussions);

        // GetTopActiveDiscussionsAsync returns null (simulating gRPC failure caught by SnakkApiClient)
        app.MockApiClient
            .GetTopActiveDiscussionsAsync(null, Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((TopActiveDiscussionsList?)null);

        app.MockApiClient
            .GetTopActiveSpacesAsync(null, Arg.Any<CancellationToken>())
            .Returns(topActiveSpaces);

        app.MockApiClient
            .GetTopContributorsAsync(null, Arg.Any<CancellationToken>())
            .Returns(topContributors);

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.GetAsync("/bff/homepage-data?offset=0&pageSize=10");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        // recentDiscussions should still be present
        var recent = body.GetProperty("recentDiscussions");
        await Assert.That(recent.GetProperty("items").GetArrayLength()).IsEqualTo(1);

        // topActiveDiscussions should be null (caught exception returned null)
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
        app.MockApiClient
            .PreviewMarkupAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("<p>This is <strong>bold</strong> text</p>");

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
        // PreviewMarkupAsync catches RpcException and returns null → BFF maps to empty string
        app.MockApiClient
            .PreviewMarkupAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((string?)null);

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
        app.MockApiClient
            .CreateReportAsync(Arg.Any<CreateReportRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ReportDto(
                "report-001", "Pending", "test-user-001",
                null, null, null, null, null,
                DateTime.UtcNow, null, null, null));

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

        // Verify the API client received the call
        await app.MockApiClient.Received(1).CreateReportAsync(Arg.Any<CreateReportRequest>(), Arg.Any<CancellationToken>());
    }

    // ==================== Report Reasons ====================

    [Test]
    public async Task GetReportReasons_ReturnsReasonsFromApi()
    {
        // Arrange
        await using var app = new TestWebApp();
        var reasons = new List<ReportReasonDto>
        {
            new("reason-001", "Spam", "Unsolicited content", null, null, null, 1),
            new("reason-002", "Harassment", "Abusive behavior", null, null, null, 2),
            new("reason-003", "Off-topic", "Irrelevant content", null, null, null, 3)
        };

        app.MockApiClient
            .GetReportReasonsAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(reasons);

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
        // GetReportReasonsAsync catches RpcException and returns null → BFF returns empty array
        app.MockApiClient
            .GetReportReasonsAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns((IEnumerable<ReportReasonDto>?)null);

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
        app.MockApiClient
            .BatchUpdateReadStatesAsync(Arg.Any<List<(string, string)>>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.PostAsJsonAsync("/bff/read-states/batch", new
        {
            updates = new[]
            {
                new { discussionId = "disc-001", postId = "post-010" },
                new { discussionId = "disc-002", postId = "post-020" }
            }
        });

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        // Verify the API client received the batch request
        await app.MockApiClient.Received(1).BatchUpdateReadStatesAsync(Arg.Is<List<(string, string)>>(l => l.Count == 2), Arg.Any<CancellationToken>());
    }

    // ==================== Set Space Follow Level ====================

    [Test]
    public async Task SetSpaceFollowLevel_ReturnsUpdatedFollowResult()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiClient
            .SetSpaceFollowLevelAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new FollowLevelResponse { Level = "AllActivity" });

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
        // SetSpaceFollowLevelAsync catches RpcException and returns null → BFF returns BadRequest
        app.MockApiClient
            .SetSpaceFollowLevelAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((FollowLevelResponse?)null);

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.PutAsync("/bff/spaces/space-001/follow-level?level=AllActivity", null);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }
}
