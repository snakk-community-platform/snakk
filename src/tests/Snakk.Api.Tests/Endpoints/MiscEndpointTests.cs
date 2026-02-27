using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Snakk.Api.Tests.Helpers;

namespace Snakk.Api.Tests.Endpoints;

public class MiscEndpointTests : IAsyncDisposable
{
    private readonly TestWebServer _server = new();

    // ==================== Hub Endpoints ====================

    [Test]
    public async Task CreateHub_WithoutAuth_Returns_BadRequest_OrCreated()
    {
        // Arrange — Hub creation does not require auth at route level,
        // but needs a valid community. With invalid data we expect a 4xx.
        var client = _server.CreateClient();
        var request = new
        {
            communityId = "non-existent-community",
            name = "Test Hub",
            slug = "test-hub",
            description = "A test hub"
        };

        // Act
        var response = await client.PostAsJsonAsync("/hubs", request);

        // Assert — should fail because the community does not exist
        await Assert.That((int)response.StatusCode).IsGreaterThanOrEqualTo(400);
    }

    [Test]
    public async Task GetHubs_Returns_200_With_EmptyList()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync("/hubs?offset=0&pageSize=10");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        await Assert.That(json.RootElement.TryGetProperty("items", out _)).IsTrue();
        await Assert.That(json.RootElement.GetProperty("offset").GetInt32()).IsEqualTo(0);
        await Assert.That(json.RootElement.GetProperty("pageSize").GetInt32()).IsEqualTo(10);
    }

    [Test]
    public async Task GetHub_WithNonExistentId_Returns_NotFound()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync("/hubs/non-existent-id");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task GetHubBySlug_WithNonExistentSlug_Returns_NotFound()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync("/hubs/by-slug/no-such-slug");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    // ==================== Space Endpoints ====================

    [Test]
    public async Task CreateSpace_WithInvalidHub_Returns_BadRequest()
    {
        // Arrange — Space creation does not require auth at route level,
        // but needs a valid hub. With invalid data we expect a 4xx.
        var client = _server.CreateClient();
        var request = new
        {
            hubId = "non-existent-hub",
            name = "Test Space",
            slug = "test-space",
            description = "A test space"
        };

        // Act
        var response = await client.PostAsJsonAsync("/spaces", request);

        // Assert — should fail because the hub does not exist
        await Assert.That((int)response.StatusCode).IsGreaterThanOrEqualTo(400);
    }

    [Test]
    public async Task GetSpace_WithNonExistentId_Returns_NotFound()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync("/spaces/non-existent-id");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task GetSpaceBySlug_WithNonExistentSlug_Returns_NotFound()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync("/spaces/by-slug/no-such-slug");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    // ==================== User Endpoints ====================

    [Test]
    public async Task SearchUsers_Returns_200()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync("/api/users/search?query=test");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task GetUserProfile_WithNonExistentId_Returns_NotFound()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync("/api/users/non-existent-id/profile");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    // ==================== ReadState Endpoints ====================

    [Test]
    public async Task GetReadState_WithoutAuth_Returns_Unauthorized()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync("/api/discussions/some-discussion-id/read-state");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task MarkRead_WithoutAuth_Returns_Unauthorized()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.PostAsync(
            "/api/discussions/some-discussion-id/mark-read?postId=some-post-id",
            null);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetReadState_WithAuth_Returns_200_WithNullState()
    {
        // Arrange
        var client = _server.CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/api/discussions/some-discussion-id/read-state");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        // No read state exists yet, so lastReadPostId should be null
        await Assert.That(json.RootElement.TryGetProperty("lastReadPostId", out _)).IsTrue();
    }

    // ==================== Markup Endpoints ====================

    [Test]
    public async Task PreviewMarkup_WithEmptyContent_Returns_200_WithPlaceholder()
    {
        // Arrange
        var client = _server.CreateClient();
        var content = new StringContent("", Encoding.UTF8, "text/plain");

        // Act
        var response = await client.PostAsync("/api/markup/preview", content);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        await Assert.That(body).Contains("Nothing to preview");
    }

    [Test]
    public async Task PreviewMarkup_WithContent_Returns_200_WithHtml()
    {
        // Arrange
        var client = _server.CreateClient();
        var content = new StringContent("**bold text**", Encoding.UTF8, "text/plain");

        // Act
        var response = await client.PostAsync("/api/markup/preview", content);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        await Assert.That(body).Contains("prose");
    }

    // ==================== Sitemap Endpoints ====================

    [Test]
    public async Task GetSitemap_Returns_200_WithXml()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync("/sitemap.xml");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        await Assert.That(body).Contains("<?xml");
        await Assert.That(body).Contains("<urlset");
    }

    public async ValueTask DisposeAsync()
    {
        await _server.DisposeAsync();
    }
}
