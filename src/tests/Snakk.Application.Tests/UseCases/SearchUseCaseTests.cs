using NSubstitute;
using Snakk.Application.Repositories;
using Snakk.Application.UseCases;
using Snakk.Shared.Models;

namespace Snakk.Application.Tests.UseCases;

public class SearchUseCaseTests
{
    private readonly ISearchRepository _searchRepository = Substitute.For<ISearchRepository>();
    private SearchUseCase _useCase = null!;

    [Before(Test)]
    public void Setup()
    {
        _useCase = new SearchUseCase(_searchRepository);
    }

    #region SearchDiscussionsAsync Tests

    [Test]
    public async Task SearchDiscussionsAsync_WithQuery_ReturnsResults()
    {
        const string query = "test topic";
        var results = new List<RecentDiscussionDto>
        {
            new("disc-1", "Test Topic Discussion", "test-topic-discussion",
                0, DateTime.UtcNow, DateTime.UtcNow, false, false,
                "space-1", "general", "General", "hub-1", "main-hub", "Main Hub",
                "comm-1", "snakk", "Snakk", "user-1", "TestUser", null, null, 5, 3, []),
            new("disc-2", "Another Test Topic", "another-test-topic",
                0, DateTime.UtcNow, null, false, false,
                "space-2", "off-topic", "Off-Topic", "hub-1", "main-hub", "Main Hub",
                "comm-1", "snakk", "Snakk", "user-2", "User2", null, null, 2, 1, [])
        };
        var pagedResult = new PagedResult<RecentDiscussionDto> { Items = results, Offset = 0, PageSize = 20, HasMoreItems = false };
        _searchRepository.SearchDiscussionsAsync(query, null, null, null, 0, 20, null).Returns(pagedResult);

        var result = await _useCase.SearchDiscussionsAsync(query);

        await Assert.That(result).IsNotNull();
        await Assert.That(result.Items).Count().IsEqualTo(2);
        await Assert.That(result.HasMoreItems).IsFalse();
    }

    [Test]
    public async Task SearchDiscussionsAsync_WithAuthorFilter_PassesAuthorId()
    {
        const string query = "test";
        const string authorId = "author-1";
        var pagedResult = new PagedResult<RecentDiscussionDto> { Items = [], Offset = 0, PageSize = 20, HasMoreItems = false };
        _searchRepository.SearchDiscussionsAsync(query, authorId, null, null, 0, 20, null).Returns(pagedResult);

        var result = await _useCase.SearchDiscussionsAsync(query, authorPublicId: authorId);

        await _searchRepository.Received(1).SearchDiscussionsAsync(query, authorId, null, null, 0, 20, null);
    }

    [Test]
    public async Task SearchDiscussionsAsync_WithSpaceFilter_PassesSpaceId()
    {
        const string query = "test";
        const string spaceId = "space-1";
        var pagedResult = new PagedResult<RecentDiscussionDto> { Items = [], Offset = 0, PageSize = 20, HasMoreItems = false };
        _searchRepository.SearchDiscussionsAsync(query, null, spaceId, null, 0, 20, null).Returns(pagedResult);

        var result = await _useCase.SearchDiscussionsAsync(query, spacePublicId: spaceId);

        await _searchRepository.Received(1).SearchDiscussionsAsync(query, null, spaceId, null, 0, 20, null);
    }

    [Test]
    public async Task SearchDiscussionsAsync_WithHubFilter_PassesHubId()
    {
        const string query = "test";
        const string hubId = "hub-1";
        var pagedResult = new PagedResult<RecentDiscussionDto> { Items = [], Offset = 0, PageSize = 20, HasMoreItems = false };
        _searchRepository.SearchDiscussionsAsync(query, null, null, hubId, 0, 20, null).Returns(pagedResult);

        var result = await _useCase.SearchDiscussionsAsync(query, hubPublicId: hubId);

        await _searchRepository.Received(1).SearchDiscussionsAsync(query, null, null, hubId, 0, 20, null);
    }

    [Test]
    public async Task SearchDiscussionsAsync_WithPagination_PassesPaginationParameters()
    {
        var pagedResult = new PagedResult<RecentDiscussionDto> { Items = [], Offset = 10, PageSize = 5, HasMoreItems = true };
        _searchRepository.SearchDiscussionsAsync("query", null, null, null, 10, 5, null).Returns(pagedResult);

        var result = await _useCase.SearchDiscussionsAsync("query", offset: 10, pageSize: 5);

        await Assert.That(result.Offset).IsEqualTo(10);
        await Assert.That(result.PageSize).IsEqualTo(5);
        await Assert.That(result.HasMoreItems).IsTrue();
    }

    #endregion

    #region SearchPostsAsync Tests

    [Test]
    public async Task SearchPostsAsync_WithQuery_ReturnsResults()
    {
        const string query = "search term";
        var results = new List<PostSearchResultDto>
        {
            new("post-1", "<p>This contains the search term</p>", "user-1", "TestUser", null,
                "disc-1", "Discussion Title", "discussion-title", "general", "General", "main-hub", "Main Hub", "snakk", DateTime.UtcNow)
        };
        var pagedResult = new PagedResult<PostSearchResultDto> { Items = results, Offset = 0, PageSize = 20, HasMoreItems = false };
        _searchRepository.SearchPostsAsync(query, null, null, null, 0, 20, null).Returns(pagedResult);

        var result = await _useCase.SearchPostsAsync(query);

        await Assert.That(result).IsNotNull();
        await Assert.That(result.Items).Count().IsEqualTo(1);
    }

    [Test]
    public async Task SearchPostsAsync_WithAuthorFilter_PassesAuthorId()
    {
        var pagedResult = new PagedResult<PostSearchResultDto> { Items = [], Offset = 0, PageSize = 20, HasMoreItems = false };
        _searchRepository.SearchPostsAsync("query", "author-1", null, null, 0, 20, null).Returns(pagedResult);

        await _useCase.SearchPostsAsync("query", authorPublicId: "author-1");

        await _searchRepository.Received(1).SearchPostsAsync("query", "author-1", null, null, 0, 20);
    }

    [Test]
    public async Task SearchPostsAsync_WithDiscussionFilter_PassesDiscussionId()
    {
        var pagedResult = new PagedResult<PostSearchResultDto> { Items = [], Offset = 0, PageSize = 20, HasMoreItems = false };
        _searchRepository.SearchPostsAsync("query", null, "disc-1", null, 0, 20, null).Returns(pagedResult);

        await _useCase.SearchPostsAsync("query", discussionPublicId: "disc-1");

        await _searchRepository.Received(1).SearchPostsAsync("query", null, "disc-1", null, 0, 20);
    }

    [Test]
    public async Task SearchPostsAsync_WithSpaceFilter_PassesSpaceId()
    {
        var pagedResult = new PagedResult<PostSearchResultDto> { Items = [], Offset = 0, PageSize = 20, HasMoreItems = false };
        _searchRepository.SearchPostsAsync("query", null, null, "space-1", 0, 20, null).Returns(pagedResult);

        await _useCase.SearchPostsAsync("query", spacePublicId: "space-1");

        await _searchRepository.Received(1).SearchPostsAsync("query", null, null, "space-1", 0, 20);
    }

    [Test]
    public async Task SearchPostsAsync_WithPagination_PassesPaginationParameters()
    {
        var pagedResult = new PagedResult<PostSearchResultDto> { Items = [], Offset = 20, PageSize = 10, HasMoreItems = false };
        _searchRepository.SearchPostsAsync("query", null, null, null, 20, 10, null).Returns(pagedResult);

        var result = await _useCase.SearchPostsAsync("query", offset: 20, pageSize: 10);

        await Assert.That(result.Offset).IsEqualTo(20);
        await Assert.That(result.PageSize).IsEqualTo(10);
    }

    #endregion
}
