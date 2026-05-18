using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using NSubstitute;
using Snakk.Protos.Community;
using Snakk.Protos.User;
using Snakk.Web.Tests.Helpers;

namespace Snakk.Web.Tests.Endpoints;

/// <summary>
/// Integration tests for the oEmbed endpoint:
///   GET /oembed
/// </summary>
public class BffOEmbedTests
{
    [Test]
    public async Task OEmbed_NoUrl_ReturnsNotFound()
    {
        // Arrange
        await using var app = new TestWebApp();
        var client = app.CreateClient();

        // Act
        var response = await client.GetAsync("/oembed");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task OEmbed_FormatXml_Returns501()
    {
        // Arrange
        await using var app = new TestWebApp();
        var client = app.CreateClient();

        // Act
        var response = await client.GetAsync("/oembed?url=http://localhost/c/test&format=xml");

        // Assert
        await Assert.That((int)response.StatusCode).IsEqualTo(501);
    }

    [Test]
    public async Task OEmbed_NonAbsoluteUrl_ReturnsNotFound()
    {
        // Arrange
        await using var app = new TestWebApp();
        var client = app.CreateClient();

        // Act — relative URL, not parseable as absolute URI
        var response = await client.GetAsync("/oembed?url=/c/test-community");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task OEmbed_UnrecognizedUrlPattern_ReturnsNotFound()
    {
        // Arrange
        await using var app = new TestWebApp();
        var client = app.CreateClient();

        // Act — valid absolute URL but no known entity pattern
        var response = await client.GetAsync("/oembed?url=http://localhost/some/unknown/path");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task OEmbed_ValidCommunityUrl_Returns200WithOEmbedFields()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiClient
            .GetCommunityBySlugAsync("test-community", Arg.Any<CancellationToken>())
            .Returns(new CommunityInfo { Name = "Test Community", IsRestricted = false });

        var client = app.CreateClient();

        // Act
        var response = await client.GetAsync("/oembed?url=http://localhost/c/test-community");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(body.GetProperty("type").GetString()).IsEqualTo("rich");
        await Assert.That(body.GetProperty("version").GetString()).IsEqualTo("1.0");
        await Assert.That(body.GetProperty("title").GetString()).IsEqualTo("Test Community");
        await Assert.That(body.GetProperty("provider_name").GetString()).IsEqualTo("Snakk");
    }

    [Test]
    public async Task OEmbed_CommunityNotFound_ReturnsNotFound()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiClient
            .GetCommunityBySlugAsync("unknown-community", Arg.Any<CancellationToken>())
            .Returns((CommunityInfo?)null);

        var client = app.CreateClient();

        // Act
        var response = await client.GetAsync("/oembed?url=http://localhost/c/unknown-community");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task OEmbed_RestrictedCommunity_ReturnsNotFound()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiClient
            .GetCommunityBySlugAsync("restricted-community", Arg.Any<CancellationToken>())
            .Returns(new CommunityInfo { Name = "Restricted", IsRestricted = true });

        var client = app.CreateClient();

        // Act
        var response = await client.GetAsync("/oembed?url=http://localhost/c/restricted-community");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task OEmbed_ValidUserProfileUrl_Returns200WithDescription()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiClient
            .GetUserProfileAsync("user-pub-id", Arg.Any<CancellationToken>())
            .Returns(new UserProfileInfo { DisplayName = "Test User", DiscussionCount = 5, PostCount = 10 });

        var client = app.CreateClient();

        // Act
        var response = await client.GetAsync("/oembed?url=http://localhost/u/user-pub-id");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(body.GetProperty("title").GetString()).IsEqualTo("Test User");
        var description = body.GetProperty("description").GetString();
        await Assert.That(description).Contains("discussions");
        await Assert.That(description).Contains("posts");
    }

    [Test]
    public async Task OEmbed_UserProfileNotFound_ReturnsNotFound()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiClient
            .GetUserProfileAsync("missing-user", Arg.Any<CancellationToken>())
            .Returns((UserProfileInfo?)null);

        var client = app.CreateClient();

        // Act
        var response = await client.GetAsync("/oembed?url=http://localhost/u/missing-user");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }
}
