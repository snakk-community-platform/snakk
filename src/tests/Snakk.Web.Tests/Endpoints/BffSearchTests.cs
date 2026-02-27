using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Snakk.Web.Tests.Helpers;

namespace Snakk.Web.Tests.Endpoints;

/// <summary>
/// Integration tests for BFF search endpoints:
///   GET /bff/search/discussions
///   GET /bff/search/posts
/// </summary>
public class BffSearchTests
{
    [Test]
    public async Task SearchDiscussions_ReturnsResultsFromApi()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiHandler.SetupJsonResponse("/api/search/discussions", new
        {
            items = new[]
            {
                new
                {
                    publicId = "disc-001",
                    title = "Test Discussion",
                    slug = "test-discussion",
                    authorPublicId = "user-001",
                    authorDisplayName = "Test User",
                    authorAvatarFileName = (string?)null,
                    spacePublicId = "space-001",
                    spaceName = "General",
                    spaceSlug = "general",
                    hubSlug = "main-hub",
                    createdAt = "2026-02-25T10:00:00Z",
                    lastActivityAt = "2026-02-25T12:00:00Z",
                    postCount = 5,
                    reactionCount = 2
                }
            },
            offset = 0,
            pageSize = 10,
            hasMoreItems = false
        });

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.GetAsync("/bff/search/discussions?authorPublicId=user-001&pageSize=10");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items");
        await Assert.That(items.GetArrayLength()).IsEqualTo(1);
        await Assert.That(items[0].GetProperty("title").GetString()).IsEqualTo("Test Discussion");
    }

    [Test]
    public async Task SearchDiscussions_WhenApiReturnsNull_ReturnsEmptyItems()
    {
        // Arrange
        await using var app = new TestWebApp();
        // SnakkApiClient.SearchDiscussionsAsync catches errors and returns null
        app.MockApiHandler.SetupResponse("/api/search/discussions", HttpStatusCode.InternalServerError);

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.GetAsync("/bff/search/discussions?pageSize=10");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items");
        await Assert.That(items.GetArrayLength()).IsEqualTo(0);
    }

    [Test]
    public async Task SearchPosts_ReturnsResultsFromApi()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiHandler.SetupJsonResponse("/api/search/posts", new
        {
            items = new[]
            {
                new
                {
                    publicId = "post-001",
                    content = "This is a test post content",
                    authorPublicId = "user-001",
                    authorDisplayName = "Test User",
                    authorAvatarFileName = (string?)null,
                    discussionPublicId = "disc-001",
                    discussionTitle = "Test Discussion",
                    discussionSlug = "test-discussion",
                    spaceSlug = "general",
                    hubSlug = "main-hub",
                    createdAt = "2026-02-25T10:00:00Z"
                },
                new
                {
                    publicId = "post-002",
                    content = "Another post matching the search",
                    authorPublicId = "user-001",
                    authorDisplayName = "Test User",
                    authorAvatarFileName = (string?)null,
                    discussionPublicId = "disc-002",
                    discussionTitle = "Another Discussion",
                    discussionSlug = "another-discussion",
                    spaceSlug = "general",
                    hubSlug = "main-hub",
                    createdAt = "2026-02-25T11:00:00Z"
                }
            },
            offset = 0,
            pageSize = 10,
            hasMoreItems = false
        });

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.GetAsync("/bff/search/posts?authorPublicId=user-001&pageSize=10");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items");
        await Assert.That(items.GetArrayLength()).IsEqualTo(2);
        await Assert.That(items[0].GetProperty("publicId").GetString()).IsEqualTo("post-001");
        await Assert.That(items[1].GetProperty("publicId").GetString()).IsEqualTo("post-002");
    }

    [Test]
    public async Task SearchPosts_WhenApiReturnsNull_ReturnsEmptyItems()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiHandler.SetupResponse("/api/search/posts", HttpStatusCode.InternalServerError);

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.GetAsync("/bff/search/posts?pageSize=10");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items");
        await Assert.That(items.GetArrayLength()).IsEqualTo(0);
    }

    [Test]
    public async Task SearchDiscussions_PassesAuthorFilterToApi()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiHandler.SetupJsonResponse("/api/search/discussions", new
        {
            items = Array.Empty<object>(),
            offset = 0,
            pageSize = 20,
            hasMoreItems = false
        });

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        await client.GetAsync("/bff/search/discussions?authorPublicId=user-123&pageSize=20");

        // Assert - verify the API received the correct query parameters
        var apiCalls = app.MockApiHandler.ReceivedRequests
            .Where(r => r.RequestUri?.PathAndQuery?.Contains("/api/search/discussions") == true)
            .ToList();
        await Assert.That(apiCalls.Count).IsEqualTo(1);

        var apiUri = apiCalls[0].RequestUri?.ToString() ?? "";
        await Assert.That(apiUri.Contains("authorPublicId=user-123")).IsTrue();
        await Assert.That(apiUri.Contains("pageSize=20")).IsTrue();
    }

    [Test]
    public async Task SearchPosts_PassesAuthorFilterToApi()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiHandler.SetupJsonResponse("/api/search/posts", new
        {
            items = Array.Empty<object>(),
            offset = 0,
            pageSize = 15,
            hasMoreItems = false
        });

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        await client.GetAsync("/bff/search/posts?authorPublicId=user-456&pageSize=15");

        // Assert
        var apiCalls = app.MockApiHandler.ReceivedRequests
            .Where(r => r.RequestUri?.PathAndQuery?.Contains("/api/search/posts") == true)
            .ToList();
        await Assert.That(apiCalls.Count).IsEqualTo(1);

        var apiUri = apiCalls[0].RequestUri?.ToString() ?? "";
        await Assert.That(apiUri.Contains("authorPublicId=user-456")).IsTrue();
        await Assert.That(apiUri.Contains("pageSize=15")).IsTrue();
    }
}
