using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Moq;
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
            .ReturnsAsync(new Dictionary<string, int>
            {
                { "Agree", 5 },
                { "Love", 3 },
                { "Watching", 1 }
            });

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.GetAsync("/bff/posts/post-001/reactions");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var counts = body.GetProperty("counts");
        await Assert.That(counts.GetProperty("Agree").GetInt32()).IsEqualTo(5);
        await Assert.That(counts.GetProperty("Love").GetInt32()).IsEqualTo(3);
        await Assert.That(counts.GetProperty("Watching").GetInt32()).IsEqualTo(1);
    }

    [Test]
    public async Task GetPostReactions_WhenApiFails_ReturnsAllZeros()
    {
        // Arrange
        await using var app = new TestWebApp();
        // When API returns null, BFF endpoint defaults all counts to empty
        app.MockApiClient
            .Setup(c => c.GetPostReactionsAsync(It.IsAny<string>()))
            .ReturnsAsync((Dictionary<string, int>?)null);

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.GetAsync("/bff/posts/post-001/reactions");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var counts = body.GetProperty("counts");
        await Assert.That(counts.EnumerateObject().Count()).IsEqualTo(0);
    }

    [Test]
    public async Task GetMyPostReactions_ReturnsCurrentUserReactions()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiClient
            .Setup(c => c.GetMyPostReactionsAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<string> { "Love" });

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.GetAsync("/bff/posts/post-001/reactions/me");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var reactions = body.GetProperty("reactions");
        await Assert.That(reactions.GetArrayLength()).IsEqualTo(1);
        await Assert.That(reactions[0].GetString()).IsEqualTo("Love");
    }

    [Test]
    public async Task GetMyPostReactions_WhenNoReaction_ReturnsEmptyList()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiClient
            .Setup(c => c.GetMyPostReactionsAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<string>());

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.GetAsync("/bff/posts/post-001/reactions/me");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var reactions = body.GetProperty("reactions");
        await Assert.That(reactions.GetArrayLength()).IsEqualTo(0);
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
        var request = new { type = 1 }; // 1 = agree

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

        // Test with "love" reaction type (2)
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
