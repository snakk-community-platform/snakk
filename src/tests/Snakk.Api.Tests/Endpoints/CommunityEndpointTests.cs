using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Snakk.Api.Tests.Helpers;

namespace Snakk.Api.Tests.Endpoints;

public class CommunityEndpointTests : IAsyncDisposable
{
    private readonly TestWebServer _server = new();

    [Test]
    public async Task GetCommunities_Returns_200_With_EmptyList()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync("/communities?offset=0&pageSize=10");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        await Assert.That(json.RootElement.TryGetProperty("items", out _)).IsTrue();
        await Assert.That(json.RootElement.GetProperty("offset").GetInt32()).IsEqualTo(0);
        await Assert.That(json.RootElement.GetProperty("pageSize").GetInt32()).IsEqualTo(10);
    }

    [Test]
    public async Task GetCommunity_WithNonExistentId_Returns_NotFound()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync("/communities/non-existent-id");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task CreateCommunity_WithValidData_Returns_201_Created()
    {
        // Arrange
        var client = _server.CreateClient();
        var request = new
        {
            name = "Test Community",
            slug = "test-community",
            description = "A test community for integration tests"
        };

        // Act
        var response = await client.PostAsJsonAsync("/communities", request);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Created);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        await Assert.That(json.RootElement.GetProperty("name").GetString()).IsEqualTo("Test Community");
        await Assert.That(json.RootElement.GetProperty("slug").GetString()).IsEqualTo("test-community");
        await Assert.That(json.RootElement.TryGetProperty("publicId", out _)).IsTrue();
    }

    [Test]
    public async Task CreateCommunity_ThenGetById_Returns_CreatedCommunity()
    {
        // Arrange
        var client = _server.CreateClient();
        var request = new
        {
            name = "Roundtrip Community",
            slug = "roundtrip-community",
            description = "Testing create then get"
        };

        // Create the community
        var createResponse = await client.PostAsJsonAsync("/communities", request);
        await Assert.That(createResponse.StatusCode).IsEqualTo(HttpStatusCode.Created);

        var createContent = await createResponse.Content.ReadAsStringAsync();
        var createJson = JsonDocument.Parse(createContent);
        var publicId = createJson.RootElement.GetProperty("publicId").GetString()!;

        // Act - fetch it by ID
        var response = await client.GetAsync($"/communities/{publicId}");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        await Assert.That(json.RootElement.GetProperty("name").GetString()).IsEqualTo("Roundtrip Community");
        await Assert.That(json.RootElement.GetProperty("slug").GetString()).IsEqualTo("roundtrip-community");
        await Assert.That(json.RootElement.GetProperty("publicId").GetString()).IsEqualTo(publicId);
    }

    [Test]
    public async Task GetCommunityBySlug_WithNonExistentSlug_Returns_NotFound()
    {
        // Arrange
        var client = _server.CreateClient();

        // Act
        var response = await client.GetAsync("/communities/by-slug/no-such-slug");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task CreateCommunity_ThenGetBySlug_Returns_CreatedCommunity()
    {
        // Arrange
        var client = _server.CreateClient();
        var request = new
        {
            name = "Slug Lookup Community",
            slug = "slug-lookup-test",
            description = "Testing slug-based lookup"
        };

        var createResponse = await client.PostAsJsonAsync("/communities", request);
        await Assert.That(createResponse.StatusCode).IsEqualTo(HttpStatusCode.Created);

        // Act - fetch by slug
        var response = await client.GetAsync("/communities/by-slug/slug-lookup-test");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        await Assert.That(json.RootElement.GetProperty("name").GetString()).IsEqualTo("Slug Lookup Community");
        await Assert.That(json.RootElement.GetProperty("slug").GetString()).IsEqualTo("slug-lookup-test");
    }

    [Test]
    public async Task GetCommunities_AfterCreating_Returns_CreatedCommunity_InList()
    {
        // Arrange
        var client = _server.CreateClient();
        var request = new
        {
            name = "Listed Community",
            slug = "listed-community",
            description = "Should appear in list"
        };

        var createResponse = await client.PostAsJsonAsync("/communities", request);
        await Assert.That(createResponse.StatusCode).IsEqualTo(HttpStatusCode.Created);

        // Act
        var response = await client.GetAsync("/communities?offset=0&pageSize=10");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        var items = json.RootElement.GetProperty("items");

        await Assert.That(items.GetArrayLength()).IsGreaterThanOrEqualTo(1);
    }

    public async ValueTask DisposeAsync()
    {
        await _server.DisposeAsync();
    }
}
