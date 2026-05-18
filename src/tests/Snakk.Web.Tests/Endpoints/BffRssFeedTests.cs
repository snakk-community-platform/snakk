using System.Net;
using NSubstitute;
using Snakk.Protos.Community;
using Snakk.Protos.Discussion;
using Snakk.Web.Tests.Helpers;

namespace Snakk.Web.Tests.Endpoints;

/// <summary>
/// Integration tests for RSS/Atom/JSON feed endpoints.
/// Verifies HTTP status codes and Content-Type headers for site-wide and scoped feeds.
/// </summary>
public class BffRssFeedTests
{
    // ------------------------------------------------------------------
    //  Site-wide feeds
    // ------------------------------------------------------------------

    #region Site-wide Feed Tests

    [Test]
    public async Task SiteWideFeed_Rss_Returns200WithRssContentType()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiClient
            .GetRecentDiscussionsAsync(
                Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<IReadOnlyList<string>?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new PagedRecentDiscussionList());

        var client = app.CreateClient();

        // Act
        var response = await client.GetAsync("/feed.xml");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(response.Content.Headers.ContentType?.MediaType).IsEqualTo("application/rss+xml");
    }

    [Test]
    public async Task SiteWideFeed_Atom_Returns200WithAtomContentType()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiClient
            .GetRecentDiscussionsAsync(
                Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<IReadOnlyList<string>?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new PagedRecentDiscussionList());

        var client = app.CreateClient();

        // Act
        var response = await client.GetAsync("/feed.atom");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(response.Content.Headers.ContentType?.MediaType).IsEqualTo("application/atom+xml");
    }

    [Test]
    public async Task SiteWideFeed_Json_Returns200WithJsonFeedContentType()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiClient
            .GetRecentDiscussionsAsync(
                Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<IReadOnlyList<string>?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new PagedRecentDiscussionList());

        var client = app.CreateClient();

        // Act
        var response = await client.GetAsync("/feed.json");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(response.Content.Headers.ContentType?.MediaType).IsEqualTo("application/feed+json");
    }

    [Test]
    public async Task SiteWideFeed_Rss_BodyContainsRssElement()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiClient
            .GetRecentDiscussionsAsync(
                Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<IReadOnlyList<string>?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new PagedRecentDiscussionList());

        var client = app.CreateClient();

        // Act
        var response = await client.GetAsync("/feed.xml");
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        await Assert.That(body).Contains("<rss");
        await Assert.That(body).Contains("<channel>");
    }

    [Test]
    public async Task SiteWideFeed_Atom_BodyContainsFeedElement()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiClient
            .GetRecentDiscussionsAsync(
                Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<IReadOnlyList<string>?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new PagedRecentDiscussionList());

        var client = app.CreateClient();

        // Act
        var response = await client.GetAsync("/feed.atom");
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        await Assert.That(body).Contains("<feed ");
    }

    #endregion

    // ------------------------------------------------------------------
    //  Community feeds
    // ------------------------------------------------------------------

    #region Community Feed Tests

    [Test]
    public async Task CommunityFeed_Rss_CommunityNotFound_Returns404()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiClient
            .GetCommunityBySlugAsync("unknown-comm", Arg.Any<CancellationToken>())
            .Returns((CommunityInfo?)null);

        var client = app.CreateClient();

        // Act
        var response = await client.GetAsync("/c/unknown-comm/feed.xml");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task CommunityFeed_Rss_ValidCommunity_Returns200()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiClient
            .GetCommunityBySlugAsync("my-community", Arg.Any<CancellationToken>())
            .Returns(new CommunityInfo { Name = "My Community", PublicId = "comm-feed-01" });
        app.MockApiClient
            .GetRecentDiscussionsAsync(
                Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<IReadOnlyList<string>?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new PagedRecentDiscussionList());

        var client = app.CreateClient();

        // Act
        var response = await client.GetAsync("/c/my-community/feed.xml");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(response.Content.Headers.ContentType?.MediaType).IsEqualTo("application/rss+xml");
    }

    [Test]
    public async Task CommunityFeed_Rss_ApiDiscussionsNull_Returns503()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiClient
            .GetCommunityBySlugAsync("test-503-comm", Arg.Any<CancellationToken>())
            .Returns(new CommunityInfo { Name = "Test 503 Community", PublicId = "comm-503-id" });
        app.MockApiClient
            .GetRecentDiscussionsAsync(
                Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<IReadOnlyList<string>?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((PagedRecentDiscussionList?)null);

        var client = app.CreateClient();

        // Act
        var response = await client.GetAsync("/c/test-503-comm/feed.xml");

        // Assert
        await Assert.That((int)response.StatusCode).IsEqualTo(503);
    }

    [Test]
    public async Task CommunityFeed_Atom_ValidCommunity_Returns200WithAtomContentType()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiClient
            .GetCommunityBySlugAsync("atom-community", Arg.Any<CancellationToken>())
            .Returns(new CommunityInfo { Name = "Atom Community", PublicId = "comm-atom-01" });
        app.MockApiClient
            .GetRecentDiscussionsAsync(
                Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<IReadOnlyList<string>?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new PagedRecentDiscussionList());

        var client = app.CreateClient();

        // Act
        var response = await client.GetAsync("/c/atom-community/feed.atom");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(response.Content.Headers.ContentType?.MediaType).IsEqualTo("application/atom+xml");
    }

    #endregion
}
