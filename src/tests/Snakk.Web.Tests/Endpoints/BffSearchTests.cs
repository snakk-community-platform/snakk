using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Google.Protobuf.WellKnownTypes;
using Moq;
using Snakk.Protos;
using Snakk.Protos.Search;
using Snakk.Web.Tests.Helpers;

namespace Snakk.Web.Tests.Endpoints;

/// <summary>
/// Integration tests for BFF search endpoints:
///   GET /bff/search/discussions
///   GET /bff/search/posts
/// </summary>
public class BffSearchTests
{
    [Test]
    public async Task SearchDiscussions_ReturnsResultsFromApi()
    {
        // Arrange
        await using var app = new TestWebApp();

        var searchResults = new PagedDiscussionSearchResults
        {
            Offset = 0,
            PageSize = 10,
            HasMoreItems = false
        };
        searchResults.Items.Add(new DiscussionSearchResult
        {
            PublicId = "01ARZ3NDEKTSV4RRFFQ69G5FAV",
            Title = "Test Discussion",
            Slug = "test-discussion",
            CreatedAt = Timestamp.FromDateTime(new DateTime(2026, 2, 25, 10, 0, 0, DateTimeKind.Utc)),
            PostCount = 5,
            Author = new AuthorRef
            {
                PublicId = "01BRZ3NDEKTSV4RRFFQ69G5FAV",
                DisplayName = "Test User"
            },
            Space = new EntityRef
            {
                PublicId = "01CRZ3NDEKTSV4RRFFQ69G5FAV",
                Name = "General",
                Slug = "general"
            },
            Hub = new EntityRef
            {
                Slug = "main-hub"
            }
        });

        app.MockApiClient
            .Setup(c => c.SearchDiscussionsAsync(
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(searchResults);

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.GetAsync("/bff/search/discussions?authorPublicId=user-001&pageSize=10&offset=0");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items");
        await Assert.That(items.GetArrayLength()).IsEqualTo(1);
        await Assert.That(items[0].GetProperty("title").GetString()).IsEqualTo("Test Discussion");
    }

    [Test]
    public async Task SearchDiscussions_WhenApiReturnsNull_ReturnsEmptyItems()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiClient
            .Setup(c => c.SearchDiscussionsAsync(
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync((PagedDiscussionSearchResults?)null);

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.GetAsync("/bff/search/discussions?pageSize=10&offset=0");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items");
        await Assert.That(items.GetArrayLength()).IsEqualTo(0);
    }

    [Test]
    public async Task SearchPosts_ReturnsResultsFromApi()
    {
        // Arrange
        await using var app = new TestWebApp();

        var searchResults = new PagedPostSearchResults
        {
            Offset = 0,
            PageSize = 10,
            HasMoreItems = false
        };
        searchResults.Items.Add(new PostSearchResult
        {
            PublicId = "01DRZ3NDEKTSV4RRFFQ69G5FAV",
            ContentHighlight = "This is a test post content",
            CreatedAt = Timestamp.FromDateTime(new DateTime(2026, 2, 25, 10, 0, 0, DateTimeKind.Utc)),
            Author = new AuthorRef
            {
                PublicId = "01BRZ3NDEKTSV4RRFFQ69G5FAV",
                DisplayName = "Test User"
            },
            DiscussionPublicId = "01ARZ3NDEKTSV4RRFFQ69G5FAV",
            DiscussionTitle = "Test Discussion",
            DiscussionSlug = "test-discussion",
            Space = new EntityRef { Slug = "general", Name = "General" },
            Hub = new EntityRef { Slug = "main-hub", Name = "Main Hub" }
        });
        searchResults.Items.Add(new PostSearchResult
        {
            PublicId = "01ERZ3NDEKTSV4RRFFQ69G5FAV",
            ContentHighlight = "Another post matching the search",
            CreatedAt = Timestamp.FromDateTime(new DateTime(2026, 2, 25, 11, 0, 0, DateTimeKind.Utc)),
            Author = new AuthorRef
            {
                PublicId = "01BRZ3NDEKTSV4RRFFQ69G5FAV",
                DisplayName = "Test User"
            },
            DiscussionPublicId = "01FRZ3NDEKTSV4RRFFQ69G5FAV",
            DiscussionTitle = "Another Discussion",
            DiscussionSlug = "another-discussion",
            Space = new EntityRef { Slug = "general", Name = "General" },
            Hub = new EntityRef { Slug = "main-hub", Name = "Main Hub" }
        });

        app.MockApiClient
            .Setup(c => c.SearchPostsAsync(
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(searchResults);

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.GetAsync("/bff/search/posts?authorPublicId=user-001&pageSize=10&offset=0");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items");
        await Assert.That(items.GetArrayLength()).IsEqualTo(2);
        await Assert.That(items[0].GetProperty("discussionTitle").GetString()).IsEqualTo("Test Discussion");
        await Assert.That(items[1].GetProperty("discussionTitle").GetString()).IsEqualTo("Another Discussion");
    }

    [Test]
    public async Task SearchPosts_WhenApiReturnsNull_ReturnsEmptyItems()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiClient
            .Setup(c => c.SearchPostsAsync(
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync((PagedPostSearchResults?)null);

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.GetAsync("/bff/search/posts?pageSize=10&offset=0");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items");
        await Assert.That(items.GetArrayLength()).IsEqualTo(0);
    }

    [Test]
    public async Task SearchDiscussions_PassesAuthorFilterToApi()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiClient
            .Setup(c => c.SearchDiscussionsAsync(
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(new PagedDiscussionSearchResults
            {
                Offset = 0,
                PageSize = 20,
                HasMoreItems = false
            });

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        await client.GetAsync("/bff/search/discussions?authorPublicId=user-123&pageSize=20&offset=0");

        // Assert - verify the API client received the correct parameters
        app.MockApiClient.Verify(
            c => c.SearchDiscussionsAsync(
                It.IsAny<string?>(),
                "user-123",
                null,
                null,
                It.IsAny<int>(),
                20),
            Times.Once);
    }

    [Test]
    public async Task SearchPosts_PassesAuthorFilterToApi()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiClient
            .Setup(c => c.SearchPostsAsync(
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(new PagedPostSearchResults
            {
                Offset = 0,
                PageSize = 15,
                HasMoreItems = false
            });

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        await client.GetAsync("/bff/search/posts?authorPublicId=user-456&pageSize=15&offset=0");

        // Assert - verify the API client received the correct parameters
        app.MockApiClient.Verify(
            c => c.SearchPostsAsync(
                It.IsAny<string?>(),
                "user-456",
                null,
                null,
                It.IsAny<int>(),
                15),
            Times.Once);
    }
}
