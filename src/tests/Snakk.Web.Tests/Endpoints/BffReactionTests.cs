using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Moq;
using Snakk.Protos.Reaction;
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
        app.MockApiClient
            .Setup(c => c.GetPostReactionsAsync(It.IsAny<string>()))
            .ReturnsAsync(new Snakk.Protos.ReactionCounts
            {
                ThumbsUp = 5,
                Heart = 3,
                Eyes = 1,
                Crazy = 0
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
        // When API returns null, BFF endpoint defaults all counts to 0
        app.MockApiClient
            .Setup(c => c.GetPostReactionsAsync(It.IsAny<string>()))
            .ReturnsAsync((Snakk.Protos.ReactionCounts?)null);

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
        app.MockApiClient
            .Setup(c => c.GetMyPostReactionAsync(It.IsAny<string>()))
            .ReturnsAsync(new UserReactionResponse { Reaction = "heart" });

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
        // UserReactionResponse with empty/unset Reaction field maps to null in BFF response
        app.MockApiClient
            .Setup(c => c.GetMyPostReactionAsync(It.IsAny<string>()))
            .ReturnsAsync(new UserReactionResponse());

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
        app.MockApiClient
            .Setup(c => c.TogglePostReactionAsync(It.IsAny<string>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);

        var client = TestJwtHelper.CreateAuthenticatedClient(app);
        var request = new { type = 1 }; // 1 = thumbsUp

        // Act
        var response = await client.PostAsJsonAsync("/bff/posts/post-001/reactions", request);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        // Verify the API client was called with correct post ID and reaction type
        app.MockApiClient.Verify(
            c => c.TogglePostReactionAsync("post-001", 1),
            Times.Once);
    }

    [Test]
    public async Task TogglePostReaction_WithDifferentReactionTypes_ForwardsCorrectly()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiClient
            .Setup(c => c.TogglePostReactionAsync(It.IsAny<string>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Test with "heart" reaction type (2)
        var request = new { type = 2 };

        // Act
        var response = await client.PostAsJsonAsync("/bff/posts/post-001/reactions", request);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        app.MockApiClient.Verify(
            c => c.TogglePostReactionAsync("post-001", 2),
            Times.Once);
    }
}
