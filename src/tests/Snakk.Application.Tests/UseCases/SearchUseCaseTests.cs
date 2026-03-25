using Moq;
using Snakk.Application.Repositories;
using Snakk.Application.UseCases;
using Snakk.Shared.Models;

namespace Snakk.Application.Tests.UseCases;

public class SearchUseCaseTests
{
    private readonly Mock<ISearchRepository> _mockSearchRepository = new();
    private SearchUseCase _useCase = null!;

    [Before(Test)]
    public void Setup()
    {
        _useCase = new SearchUseCase(_mockSearchRepository.Object);
    }

    #region SearchDiscussionsAsync Tests

    [Test]
    public async Task SearchDiscussionsAsync_WithQuery_ReturnsResults()
    {
        // Arrange
        const string query = "test topic";
        var results = new List<DiscussionSearchResultDto>
        {
            new("disc-1", "Test Topic Discussion", "test-topic-discussion",
                "user-1", "TestUser", null, "space-1", "General", "general", "main-hub",
                DateTime.UtcNow, DateTime.UtcNow, 5, 100),
            new("disc-2", "Another Test Topic", "another-test-topic",
                "user-2", "User2", null, "space-2", "Off-Topic", "off-topic", "main-hub",
                DateTime.UtcNow, null, 2, 50)
        };

        var pagedResult = new PagedResult<DiscussionSearchResultDto>
        {
            Items = results,
            Offset = 0,
            PageSize = 20,
            HasMoreItems = false
        };

        _mockSearchRepository.Setup(r => r.SearchDiscussionsAsync(query, null, null, null, 0, 20, null))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _useCase.SearchDiscussionsAsync(query);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Items).Count().IsEqualTo(2);
        await Assert.That(result.HasMoreItems).IsFalse();
    }

    [Test]
    public async Task SearchDiscussionsAsync_WithAuthorFilter_PassesAuthorId()
    {
        // Arrange
        const string query = "test";
        const string authorId = "author-1";

        var pagedResult = new PagedResult<DiscussionSearchResultDto>
        {
            Items = [],
            Offset = 0,
            PageSize = 20,
            HasMoreItems = false
        };

        _mockSearchRepository.Setup(r => r.SearchDiscussionsAsync(query, authorId, null, null, 0, 20, null))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _useCase.SearchDiscussionsAsync(query, authorPublicId: authorId);

        // Assert
        _mockSearchRepository.Verify(r => r.SearchDiscussionsAsync(query, authorId, null, null, 0, 20, null), Times.Once);
    }

    [Test]
    public async Task SearchDiscussionsAsync_WithSpaceFilter_PassesSpaceId()
    {
        // Arrange
        const string query = "test";
        const string spaceId = "space-1";

        var pagedResult = new PagedResult<DiscussionSearchResultDto>
        {
            Items = [],
            Offset = 0,
            PageSize = 20,
            HasMoreItems = false
        };

        _mockSearchRepository.Setup(r => r.SearchDiscussionsAsync(query, null, spaceId, null, 0, 20, null))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _useCase.SearchDiscussionsAsync(query, spacePublicId: spaceId);

        // Assert
        _mockSearchRepository.Verify(r => r.SearchDiscussionsAsync(query, null, spaceId, null, 0, 20, null), Times.Once);
    }

    [Test]
    public async Task SearchDiscussionsAsync_WithHubFilter_PassesHubId()
    {
        // Arrange
        const string query = "test";
        const string hubId = "hub-1";

        var pagedResult = new PagedResult<DiscussionSearchResultDto>
        {
            Items = [],
            Offset = 0,
            PageSize = 20,
            HasMoreItems = false
        };

        _mockSearchRepository.Setup(r => r.SearchDiscussionsAsync(query, null, null, hubId, 0, 20, null))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _useCase.SearchDiscussionsAsync(query, hubPublicId: hubId);

        // Assert
        _mockSearchRepository.Verify(r => r.SearchDiscussionsAsync(query, null, null, hubId, 0, 20, null), Times.Once);
    }

    [Test]
    public async Task SearchDiscussionsAsync_WithPagination_PassesPaginationParameters()
    {
        // Arrange
        var pagedResult = new PagedResult<DiscussionSearchResultDto>
        {
            Items = [],
            Offset = 10,
            PageSize = 5,
            HasMoreItems = true
        };

        _mockSearchRepository.Setup(r => r.SearchDiscussionsAsync("query", null, null, null, 10, 5, null))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _useCase.SearchDiscussionsAsync("query", offset: 10, pageSize: 5);

        // Assert
        await Assert.That(result.Offset).IsEqualTo(10);
        await Assert.That(result.PageSize).IsEqualTo(5);
        await Assert.That(result.HasMoreItems).IsTrue();
    }

    #endregion

    #region SearchPostsAsync Tests

    [Test]
    public async Task SearchPostsAsync_WithQuery_ReturnsResults()
    {
        // Arrange
        const string query = "search term";
        var results = new List<PostSearchResultDto>
        {
            new("post-1", "This contains the search term", "user-1", "TestUser", null,
                "disc-1", "Discussion Title", "discussion-title", "general", "main-hub", DateTime.UtcNow)
        };

        var pagedResult = new PagedResult<PostSearchResultDto>
        {
            Items = results,
            Offset = 0,
            PageSize = 20,
            HasMoreItems = false
        };

        _mockSearchRepository.Setup(r => r.SearchPostsAsync(query, null, null, null, 0, 20))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _useCase.SearchPostsAsync(query);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Items).Count().IsEqualTo(1);
    }

    [Test]
    public async Task SearchPostsAsync_WithAuthorFilter_PassesAuthorId()
    {
        // Arrange
        var pagedResult = new PagedResult<PostSearchResultDto>
        {
            Items = [],
            Offset = 0,
            PageSize = 20,
            HasMoreItems = false
        };

        _mockSearchRepository.Setup(r => r.SearchPostsAsync("query", "author-1", null, null, 0, 20))
            .ReturnsAsync(pagedResult);

        // Act
        await _useCase.SearchPostsAsync("query", authorPublicId: "author-1");

        // Assert
        _mockSearchRepository.Verify(r => r.SearchPostsAsync("query", "author-1", null, null, 0, 20), Times.Once);
    }

    [Test]
    public async Task SearchPostsAsync_WithDiscussionFilter_PassesDiscussionId()
    {
        // Arrange
        var pagedResult = new PagedResult<PostSearchResultDto>
        {
            Items = [],
            Offset = 0,
            PageSize = 20,
            HasMoreItems = false
        };

        _mockSearchRepository.Setup(r => r.SearchPostsAsync("query", null, "disc-1", null, 0, 20))
            .ReturnsAsync(pagedResult);

        // Act
        await _useCase.SearchPostsAsync("query", discussionPublicId: "disc-1");

        // Assert
        _mockSearchRepository.Verify(r => r.SearchPostsAsync("query", null, "disc-1", null, 0, 20), Times.Once);
    }

    [Test]
    public async Task SearchPostsAsync_WithSpaceFilter_PassesSpaceId()
    {
        // Arrange
        var pagedResult = new PagedResult<PostSearchResultDto>
        {
            Items = [],
            Offset = 0,
            PageSize = 20,
            HasMoreItems = false
        };

        _mockSearchRepository.Setup(r => r.SearchPostsAsync("query", null, null, "space-1", 0, 20))
            .ReturnsAsync(pagedResult);

        // Act
        await _useCase.SearchPostsAsync("query", spacePublicId: "space-1");

        // Assert
        _mockSearchRepository.Verify(r => r.SearchPostsAsync("query", null, null, "space-1", 0, 20), Times.Once);
    }

    [Test]
    public async Task SearchPostsAsync_WithPagination_PassesPaginationParameters()
    {
        // Arrange
        var pagedResult = new PagedResult<PostSearchResultDto>
        {
            Items = [],
            Offset = 20,
            PageSize = 10,
            HasMoreItems = false
        };

        _mockSearchRepository.Setup(r => r.SearchPostsAsync("query", null, null, null, 20, 10))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _useCase.SearchPostsAsync("query", offset: 20, pageSize: 10);

        // Assert
        await Assert.That(result.Offset).IsEqualTo(20);
        await Assert.That(result.PageSize).IsEqualTo(10);
    }

    #endregion
}
