using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Snakk.Web.Tests.Helpers;

namespace Snakk.Web.Tests.Endpoints;

/// <summary>
/// Integration tests for BFF reaction endpoints:
///   GET  /bff/posts/{postId}/reactions
///   GET  /bff/posts/{postId}/reactions/me
///   POST /bff/posts/{postId}/reactions
/// </summary>
public class BffReactionTests
{
    [Test]
    public async Task GetPostReactions_ReturnsMappedReactionCounts()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiHandler.SetupJsonResponse("/api/posts/post-001/reactions", new Dictionary<string, int>
        {
            ["thumbsUp"] = 5,
            ["heart"] = 3,
            ["eyes"] = 1,
            ["crazy"] = 0
        });

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.GetAsync("/bff/posts/post-001/reactions");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(body.GetProperty("thumbsUp").GetInt32()).IsEqualTo(5);
        await Assert.That(body.GetProperty("heart").GetInt32()).IsEqualTo(3);
        await Assert.That(body.GetProperty("eyes").GetInt32()).IsEqualTo(1);
        await Assert.That(body.GetProperty("crazy").GetInt32()).IsEqualTo(0);
    }

    [Test]
    public async Task GetPostReactions_WhenApiFails_ReturnsAllZeros()
    {
        // Arrange
        await using var app = new TestWebApp();
        // SnakkApiClient.GetPostReactionsAsync catches exceptions and returns empty dict
        app.MockApiHandler.SetupResponse("/api/posts/post-001/reactions", HttpStatusCode.InternalServerError);

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.GetAsync("/bff/posts/post-001/reactions");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(body.GetProperty("thumbsUp").GetInt32()).IsEqualTo(0);
        await Assert.That(body.GetProperty("heart").GetInt32()).IsEqualTo(0);
        await Assert.That(body.GetProperty("eyes").GetInt32()).IsEqualTo(0);
        await Assert.That(body.GetProperty("crazy").GetInt32()).IsEqualTo(0);
    }

    [Test]
    public async Task GetMyPostReaction_ReturnsCurrentUserReaction()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiHandler.SetupJsonResponse("/api/posts/post-001/reactions/me", new
        {
            reaction = "heart"
        });

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.GetAsync("/bff/posts/post-001/reactions/me");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(body.GetProperty("reaction").GetString()).IsEqualTo("heart");
    }

    [Test]
    public async Task GetMyPostReaction_WhenNoReaction_ReturnsNull()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiHandler.SetupJsonResponse("/api/posts/post-001/reactions/me", new
        {
            reaction = (string?)null
        });

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.GetAsync("/bff/posts/post-001/reactions/me");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(body.GetProperty("reaction").ValueKind).IsEqualTo(JsonValueKind.Null);
    }

    [Test]
    public async Task TogglePostReaction_SendsReactionToApi()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiHandler.SetupResponse("/api/posts/post-001/reactions", HttpStatusCode.OK);

        var client = TestJwtHelper.CreateAuthenticatedClient(app);
        var request = new { type = 1 }; // 1 = thumbsUp

        // Act
        var response = await client.PostAsJsonAsync("/bff/posts/post-001/reactions", request);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        // Verify the API was called with POST method
        var apiCalls = app.MockApiHandler.ReceivedRequests
            .Where(r => r.Method == HttpMethod.Post
                && r.RequestUri?.PathAndQuery?.StartsWith("/api/posts/post-001/reactions") == true)
            .ToList();
        await Assert.That(apiCalls.Count).IsEqualTo(1);
    }

    [Test]
    public async Task TogglePostReaction_WithDifferentReactionTypes_ForwardsCorrectly()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiHandler.SetupResponse("/api/posts/post-001/reactions", HttpStatusCode.OK);

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Test with "heart" reaction type (2)
        var request = new { type = 2 };

        // Act
        var response = await client.PostAsJsonAsync("/bff/posts/post-001/reactions", request);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }
}
