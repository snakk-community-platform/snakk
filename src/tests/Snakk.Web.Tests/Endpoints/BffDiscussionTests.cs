using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Snakk.Web.Tests.Helpers;

namespace Snakk.Web.Tests.Endpoints;

/// <summary>
/// Integration tests for BFF discussion-related endpoints:
///   GET  /bff/discussions/recent
///   GET  /bff/spaces/{spaceId}/discussions
///   GET  /bff/discussions/{discussionId}/posts
///   POST /bff/posts/{postId}/edit
///   GET  /bff/discussions/{discussionId}/preview
///   POST /bff/discussions/{discussionId}/mark-read
/// </summary>
public class BffDiscussionTests
{
    // ==================== Recent Discussions ====================

    [Test]
    public async Task GetRecentDiscussions_ReturnsDiscussionsFromApi()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiHandler.SetupJsonResponse("/discussions/recent", new
        {
            items = new[]
            {
                new { publicId = "disc-001", title = "First Discussion", postCount = 5 },
                new { publicId = "disc-002", title = "Second Discussion", postCount = 12 }
            },
            total = 2,
            page = 0,
            pageSize = 20
        });

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.GetAsync("/bff/discussions/recent?offset=0&pageSize=20");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items");
        await Assert.That(items.GetArrayLength()).IsEqualTo(2);
        await Assert.That(items[0].GetProperty("publicId").GetString()).IsEqualTo("disc-001");
        await Assert.That(items[1].GetProperty("title").GetString()).IsEqualTo("Second Discussion");
    }

    [Test]
    public async Task GetRecentDiscussions_PassesQueryParametersToApi()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiHandler.SetupJsonResponse("/discussions/recent", new
        {
            items = Array.Empty<object>(),
            total = 0,
            page = 0,
            pageSize = 10
        });

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.GetAsync("/bff/discussions/recent?offset=20&pageSize=10&communityId=comm-001");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        // Verify the API received the correct query parameters
        var apiCall = app.MockApiHandler.ReceivedRequests
            .FirstOrDefault(r => r.RequestUri?.PathAndQuery?.Contains("/discussions/recent") == true);
        await Assert.That(apiCall).IsNotNull();
        await Assert.That(apiCall!.RequestUri!.PathAndQuery).Contains("offset=20");
        await Assert.That(apiCall.RequestUri.PathAndQuery).Contains("pageSize=10");
        await Assert.That(apiCall.RequestUri.PathAndQuery).Contains("communityId=comm-001");
    }

    // ==================== Space Discussions ====================

    [Test]
    public async Task GetSpaceDiscussions_ReturnsDiscussionsForSpace()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiHandler.SetupJsonResponse("/api/spaces/space-001/discussions", new
        {
            items = new[]
            {
                new { publicId = "disc-010", title = "Space Discussion", postCount = 3 }
            },
            total = 1,
            page = 0,
            pageSize = 20
        });

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.GetAsync("/bff/spaces/space-001/discussions?offset=0&pageSize=20");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items");
        await Assert.That(items.GetArrayLength()).IsEqualTo(1);
        await Assert.That(items[0].GetProperty("publicId").GetString()).IsEqualTo("disc-010");
    }

    [Test]
    public async Task GetSpaceDiscussions_WhenApiFails_ReturnsOkWithNull()
    {
        // Arrange
        await using var app = new TestWebApp();
        // SnakkApiClient.GetSpaceDiscussionsAsync catches exceptions and returns null
        app.MockApiHandler.SetupResponse("/api/spaces/space-bad/discussions", HttpStatusCode.InternalServerError);

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.GetAsync("/bff/spaces/space-bad/discussions?offset=0&pageSize=20");

        // Assert — BFF returns Ok(result) where result is null from the client
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    // ==================== Discussion Posts ====================

    [Test]
    public async Task GetDiscussionPosts_ReturnsPostsFromApi()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiHandler.SetupJsonResponse("/discussions/disc-001/posts", new
        {
            items = new[]
            {
                new { publicId = "post-001", content = "Hello world", authorDisplayName = "TestUser" },
                new { publicId = "post-002", content = "Reply here", authorDisplayName = "OtherUser" }
            },
            total = 2,
            page = 0,
            pageSize = 20
        });

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.GetAsync("/bff/discussions/disc-001/posts?offset=0&pageSize=20");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items");
        await Assert.That(items.GetArrayLength()).IsEqualTo(2);
        await Assert.That(items[0].GetProperty("publicId").GetString()).IsEqualTo("post-001");
    }

    [Test]
    public async Task GetDiscussionPosts_WhenApiFails_ReturnsServerError()
    {
        // Arrange
        await using var app = new TestWebApp();
        // GetFromJsonAsync throws HttpRequestException on non-success status (no try-catch in client)
        app.MockApiHandler.SetupResponse("/discussions/disc-missing/posts", HttpStatusCode.NotFound);

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.GetAsync("/bff/discussions/disc-missing/posts?offset=0&pageSize=20");

        // Assert — exception propagates, ASP.NET returns 500
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.InternalServerError);
    }

    // ==================== Edit Post ====================

    [Test]
    public async Task EditPost_WhenSuccessful_ReturnsOk()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiHandler.SetupResponse("/api/posts/post-001/edit", HttpStatusCode.OK);

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.PostAsync(
            "/bff/posts/post-001/edit?userId=test-user-001&content=Updated+content", null);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        // Verify the API was called
        var apiCalls = app.MockApiHandler.ReceivedRequests
            .Where(r => r.Method == HttpMethod.Post
                && r.RequestUri?.PathAndQuery?.Contains("/api/posts/post-001/edit") == true)
            .ToList();
        await Assert.That(apiCalls.Count).IsEqualTo(1);
    }

    [Test]
    public async Task EditPost_WhenApiFails_ReturnsBadRequest()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiHandler.SetupResponse("/api/posts/post-001/edit", HttpStatusCode.BadRequest);

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.PostAsync(
            "/bff/posts/post-001/edit?userId=test-user-001&content=Bad+content", null);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    // ==================== Discussion Preview ====================

    [Test]
    public async Task GetDiscussionPreview_ReturnsPreviewContent()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiHandler.SetupJsonResponse("/discussions/disc-001/preview", new
        {
            content = "<p>This is a preview of the discussion</p>"
        });

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.GetAsync("/bff/discussions/disc-001/preview");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(body.GetProperty("content").GetString())
            .IsEqualTo("<p>This is a preview of the discussion</p>");
    }

    [Test]
    public async Task GetDiscussionPreview_WhenApiFails_ReturnsServerError()
    {
        // Arrange
        await using var app = new TestWebApp();
        // GetFromJsonAsync throws HttpRequestException on non-success status (no try-catch in client)
        app.MockApiHandler.SetupResponse("/discussions/disc-missing/preview", HttpStatusCode.NotFound);

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.GetAsync("/bff/discussions/disc-missing/preview");

        // Assert — exception propagates, ASP.NET returns 500
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.InternalServerError);
    }

    // ==================== Mark Discussion as Read ====================

    [Test]
    public async Task MarkDiscussionAsRead_ForwardsToApiAndReturnsOk()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiHandler.SetupResponse("/api/discussions/disc-001/mark-read", HttpStatusCode.OK);

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.PostAsync(
            "/bff/discussions/disc-001/mark-read?userId=test-user-001&postId=post-005", null);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        // Verify the API received the call with correct query parameters
        var apiCall = app.MockApiHandler.ReceivedRequests
            .FirstOrDefault(r => r.Method == HttpMethod.Post
                && r.RequestUri?.PathAndQuery?.Contains("/api/discussions/disc-001/mark-read") == true);
        await Assert.That(apiCall).IsNotNull();
        await Assert.That(apiCall!.RequestUri!.PathAndQuery).Contains("userId=test-user-001");
        await Assert.That(apiCall.RequestUri.PathAndQuery).Contains("postId=post-005");
    }
}
