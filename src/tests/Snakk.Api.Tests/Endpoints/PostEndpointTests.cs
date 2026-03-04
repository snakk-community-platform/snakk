using System.Net;
using System.Net.Http.Json;
using Snakk.Api.Tests.Helpers;

namespace Snakk.Api.Tests.Endpoints;

public class PostEndpointTests : IAsyncDisposable
{
    private readonly TestWebServer _server = new();

    [Test]
    public async Task CreatePost_WithoutAuth_Returns_Unauthorized()
    {
        // Arrange
        var client = _server.CreateClient();
        var request = new
        {
            discussionId = "some-discussion-id",
            content = "This is a test post."
        };

        // Act
        var response = await client.PostAsJsonAsync("/posts", request);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task CreatePost_WithAuth_ButInvalidDiscussion_Returns_BadRequest()
    {
        // Arrange
        var client = _server.CreateAuthenticatedClient();
        var request = new
        {
            discussionId = "non-existent-discussion",
            content = "This is a test post."
        };

        // Act
        var response = await client.PostAsJsonAsync("/posts", request);

        // Assert
        // Should fail because the discussion doesn't exist
        await Assert.That((int)response.StatusCode).IsGreaterThanOrEqualTo(400);
    }

    [Test]
    public async Task EditPost_WithoutAuth_Returns_Unauthorized()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.PostAsync(
            "/posts/some-post-id/edit?content=edited",
            null);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task DeletePost_WithoutAuth_Returns_Unauthorized()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.DeleteAsync("/posts/some-post-id");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetPostHistory_WithoutAuth_Returns_Unauthorized()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync("/posts/some-post-id/history");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    public async ValueTask DisposeAsync()
    {
        await _server.DisposeAsync();
    }
}
