using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Snakk.Api.Tests.Helpers;

namespace Snakk.Api.Tests.Endpoints;

public class DiscussionEndpointTests : IAsyncDisposable
{
    private readonly TestWebServer _server = new();

    [Test]
    public async Task GetDiscussion_WithNonExistentId_Returns_NotFound()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync("/discussions/non-existent-id");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task GetRecentDiscussions_Returns_200_With_EmptyList()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync("/discussions/recent?offset=0&pageSize=10");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        await Assert.That(json.RootElement.TryGetProperty("items", out _)).IsTrue();
    }

    [Test]
    public async Task CreateDiscussion_WithoutAuth_Returns_Unauthorized()
    {
        // Arrange
        var client = _server.CreateClient();
        var request = new
        {
            spaceId = "some-space-id",
            title = "Test Discussion",
            slug = "test-discussion",
            firstPostContent = "This is the first post content."
        };

        // Act
        var response = await client.PostAsJsonAsync("/discussions", request);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task CreateDiscussion_WithAuth_ButInvalidSpace_Returns_BadRequest()
    {
        // Arrange
        var client = _server.CreateAuthenticatedClient();
        var request = new
        {
            spaceId = "non-existent-space",
            title = "Test Discussion",
            slug = "test-discussion",
            firstPostContent = "This is the first post content."
        };

        // Act
        var response = await client.PostAsJsonAsync("/discussions", request);

        // Assert
        // Should fail because the space doesn't exist
        await Assert.That((int)response.StatusCode).IsGreaterThanOrEqualTo(400);
    }

    [Test]
    public async Task GetDiscussionPosts_ForNonExistentDiscussion_Returns_NotFound()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync("/discussions/non-existent/posts?offset=0&pageSize=10");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task GetDiscussionPreview_ForNonExistentDiscussion_Returns_NotFound()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync("/discussions/non-existent/preview");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task GetTopActiveDiscussionsToday_Returns_200()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync("/discussions/top-active-today");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        await Assert.That(json.RootElement.TryGetProperty("items", out _)).IsTrue();
    }

    public async ValueTask DisposeAsync()
    {
        await _server.DisposeAsync();
    }
}
