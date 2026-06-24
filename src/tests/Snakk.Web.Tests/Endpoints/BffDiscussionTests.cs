using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Google.Protobuf.WellKnownTypes;
using NSubstitute;
using Snakk.Protos.Discussion;
using Snakk.Protos.Post;
using Snakk.Web.Services;
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

        var pagedResult = new PagedRecentDiscussionList
        {
            Offset = 0,
            PageSize = 20,
            HasMoreItems = false
        };
        pagedResult.Items.Add(new RecentDiscussionInfo
        {
            PublicId = "disc-001",
            Title = "First Discussion",
            Slug = "first-discussion",
            PostCount = 5,
            CreatedAt = Timestamp.FromDateTime(DateTime.UtcNow)
        });
        pagedResult.Items.Add(new RecentDiscussionInfo
        {
            PublicId = "disc-002",
            Title = "Second Discussion",
            Slug = "second-discussion",
            PostCount = 12,
            CreatedAt = Timestamp.FromDateTime(DateTime.UtcNow)
        });

        app.MockApiClient
            .GetRecentDiscussionsAsync(0, 20, null, Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<IReadOnlyList<string>?>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(pagedResult);

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

        var pagedResult = new PagedRecentDiscussionList
        {
            Offset = 20,
            PageSize = 10,
            HasMoreItems = false
        };

        app.MockApiClient
            .GetRecentDiscussionsAsync(20, 10, "comm-001", Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<IReadOnlyList<string>?>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(pagedResult);

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.GetAsync("/bff/discussions/recent?offset=20&pageSize=10&communityId=comm-001");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        // Verify the API client was called with the correct parameters
        await app.MockApiClient.Received(1).GetRecentDiscussionsAsync(20, 10, "comm-001", Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<IReadOnlyList<string>?>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    // ==================== Space Discussions ====================

    [Test]
    public async Task GetSpaceDiscussions_ReturnsDiscussionsForSpace()
    {
        // Arrange
        await using var app = new TestWebApp();

        var pagedResult = new PagedDiscussionBySpaceList
        {
            Offset = 0,
            PageSize = 20,
            HasMoreItems = false
        };
        pagedResult.Items.Add(new DiscussionBySpaceInfo
        {
            PublicId = "disc-010",
            SpaceId = "space-001",
            Title = "Space Discussion",
            Slug = "space-discussion",
            CreatedAt = Timestamp.FromDateTime(DateTime.UtcNow)
        });

        app.MockApiClient
            .GetSpaceDiscussionsAsync("space-001", 0, 20, Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(pagedResult);

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
    public async Task GetSpaceDiscussions_WhenApiReturnsNull_ReturnsOk()
    {
        // Arrange
        await using var app = new TestWebApp();

        // SnakkApiClient.GetSpaceDiscussionsAsync catches exceptions and returns null
        app.MockApiClient
            .GetSpaceDiscussionsAsync("space-bad", 0, 20, Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((PagedDiscussionBySpaceList?)null);

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

        var pagedResult = new PagedEnrichedPostList
        {
            Offset = 0,
            PageSize = 20,
            HasMoreItems = false
        };
        pagedResult.Items.Add(new EnrichedPostInfo
        {
            PublicId = "post-001",
            Content = "Hello world",
            Author = new Snakk.Protos.AuthorRef
            {
                DisplayName = "TestUser",
                PublicId = "user-001"
            },
            CreatedAt = Timestamp.FromDateTime(DateTime.UtcNow)
        });
        pagedResult.Items.Add(new EnrichedPostInfo
        {
            PublicId = "post-002",
            Content = "Reply here",
            Author = new Snakk.Protos.AuthorRef
            {
                DisplayName = "OtherUser",
                PublicId = "user-002"
            },
            CreatedAt = Timestamp.FromDateTime(DateTime.UtcNow)
        });

        app.MockApiClient
            .GetDiscussionPostsAsync("disc-001", 0, 20, Arg.Any<CancellationToken>())
            .Returns(pagedResult);

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
    public async Task GetDiscussionPosts_WhenApiReturnsNull_ReturnsNotFound()
    {
        // Arrange
        await using var app = new TestWebApp();

        // GetDiscussionPostsAsync returns null when no posts found
        app.MockApiClient
            .GetDiscussionPostsAsync("disc-missing", 0, 20, Arg.Any<CancellationToken>())
            .Returns((PagedEnrichedPostList?)null);

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.GetAsync("/bff/discussions/disc-missing/posts?offset=0&pageSize=20");

        // Assert — BFF returns NotFound when result is null
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    // ==================== Edit Post ====================

    [Test]
    public async Task EditPost_WhenSuccessful_ReturnsOk()
    {
        // Arrange
        await using var app = new TestWebApp();

        app.MockApiClient
            .EditPostResultAsync("post-001", "Updated content", Arg.Any<CancellationToken>())
            .Returns(GrpcResult<EditPostResponse>.Ok(new EditPostResponse
            {
                RenderedHtml = "<p>Updated content</p>"
            }));

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.PostAsJsonAsync(
            "/bff/posts/post-001/edit", new { Content = "Updated content" });

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        await app.MockApiClient.Received(1).EditPostResultAsync("post-001", "Updated content", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task EditPost_WhenInvalidArgument_ReturnsBadRequest()
    {
        // Arrange
        await using var app = new TestWebApp();

        // InvalidArgument maps to BadRequest via MapGrpcError
        app.MockApiClient
            .EditPostResultAsync("post-001", "Bad content", Arg.Any<CancellationToken>())
            .Returns(GrpcResult<EditPostResponse>.InvalidArgument("Content is invalid"));

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.PostAsJsonAsync(
            "/bff/posts/post-001/edit", new { Content = "Bad content" });

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task EditPost_WhenServerError_Returns503()
    {
        // Arrange
        await using var app = new TestWebApp();

        // ServerError maps to 503 via MapGrpcError
        app.MockApiClient
            .EditPostResultAsync("post-001", "Some content", Arg.Any<CancellationToken>())
            .Returns(GrpcResult<EditPostResponse>.ServerError("Internal error"));

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.PostAsJsonAsync(
            "/bff/posts/post-001/edit", new { Content = "Some content" });

        // Assert
        await Assert.That((int)response.StatusCode).IsEqualTo(503);
    }

    // ==================== Discussion Preview ====================

    [Test]
    public async Task GetDiscussionPreview_ReturnsPreviewContent()
    {
        // Arrange
        await using var app = new TestWebApp();

        app.MockApiClient
            .GetDiscussionPreviewAsync("disc-001", Arg.Any<CancellationToken>())
            .Returns(new DiscussionPreviewInfo
            {
                Content = "<p>This is a preview of the discussion</p>"
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
    public async Task GetDiscussionPreview_WhenApiReturnsNull_ReturnsNotFound()
    {
        // Arrange
        await using var app = new TestWebApp();

        // GetDiscussionPreviewAsync returns null when discussion not found
        app.MockApiClient
            .GetDiscussionPreviewAsync("disc-missing", Arg.Any<CancellationToken>())
            .Returns((DiscussionPreviewInfo?)null);

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.GetAsync("/bff/discussions/disc-missing/preview");

        // Assert — BFF returns NotFound when apiResult is null
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    // ==================== Mark Discussion as Read ====================

    [Test]
    public async Task MarkDiscussionAsRead_ForwardsToApiAndReturnsOk()
    {
        // Arrange
        await using var app = new TestWebApp();

        app.MockApiClient
            .MarkDiscussionAsReadAsync("disc-001", "test-user-001", "post-005", Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.PostAsync(
            "/bff/discussions/disc-001/mark-read?userId=test-user-001&postId=post-005", null);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        // Verify the API client was called with the correct parameters
        await app.MockApiClient.Received(1).MarkDiscussionAsReadAsync("disc-001", "test-user-001", "post-005", Arg.Any<CancellationToken>());
    }
}
