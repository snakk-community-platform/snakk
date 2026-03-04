using System.Net;
using System.Text.Json;
using Snakk.Api.Tests.Helpers;

namespace Snakk.Api.Tests.Endpoints;

public class SearchEndpointTests : IAsyncDisposable
{
    private readonly TestWebServer _server = new();

    // ── Search discussions ───────────────────────────────────────────

    [Test]
    public async Task SearchDiscussions_Returns_200()
    {
        // Arrange — public endpoint, no auth required
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync("/search/discussions?q=test&offset=0&pageSize=20");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task SearchDiscussions_WithEmptyQuery_Returns_200()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync("/search/discussions?q=&offset=0&pageSize=20");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task SearchDiscussions_WithoutQuery_Returns_200()
    {
        // Arrange — q is optional, defaults to ""
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync("/search/discussions?offset=0&pageSize=20");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    // ── Search posts ─────────────────────────────────────────────────

    [Test]
    public async Task SearchPosts_Returns_200()
    {
        // Arrange — public endpoint, no auth required
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync("/search/posts?q=test&offset=0&pageSize=20");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task SearchPosts_WithEmptyQuery_Returns_200()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync("/search/posts?q=&offset=0&pageSize=20");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task SearchPosts_WithoutQuery_Returns_200()
    {
        // Arrange — q is optional, defaults to ""
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync("/search/posts?offset=0&pageSize=20");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    public async ValueTask DisposeAsync()
    {
        await _server.DisposeAsync();
    }
}
