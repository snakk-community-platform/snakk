using NSubstitute;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database.Repositories;
using Snakk.Infrastructure.Tests.Helpers;

namespace Snakk.Infrastructure.Tests.Repositories;

/// <summary>
/// Integration tests for SearchRepository using SQLite in-memory database.
///
/// Note: SearchDiscussionsAsync and SearchPostsAsync use EF.Functions.ILike (PostgreSQL-specific)
/// and cannot be tested against SQLite. Those methods require PostgreSQL integration tests.
/// The remaining methods (count queries, listing queries, sitemap) are fully testable here.
/// </summary>
public class SearchRepositoryIntegrationTests : IDisposable
{
    private readonly SqliteTestDatabase _db;
    private readonly TestDataBuilder _builder;
    private readonly SearchRepository _repository;

    public SearchRepositoryIntegrationTests()
    {
        _db = new SqliteTestDatabase();
        _builder = new TestDataBuilder(_db.Context);
        var mockGrants = Substitute.For<IUserGrantsCacheService>();
        mockGrants.GetGrantsAsync(Arg.Any<string>())
            .Returns(new UserGrants([], [], []));
        _repository = new SearchRepository(_db.Context, mockGrants);
    }

    public void Dispose() => _db.Dispose();

    #region GetDiscussionCountByAuthorAsync Tests

    [Test]
    public async Task GetDiscussionCountByAuthorAsync_UserWithDiscussions_ReturnsCorrectCount()
    {
        // Arrange
        var (user, community, hub, space, discussion, firstPost) = await _builder.CreateFullHierarchyAsync();
        await _builder.CreateDiscussionAsync(space.Id, user.Id, "Second Discussion");
        await _builder.CreateDiscussionAsync(space.Id, user.Id, "Third Discussion");

        // Act
        var count = await _repository.GetDiscussionCountByAuthorAsync(user.PublicId);

        // Assert
        await Assert.That(count).IsEqualTo(3);
    }

    [Test]
    public async Task GetDiscussionCountByAuthorAsync_UserWithNoDiscussions_ReturnsZero()
    {
        // Arrange
        var user = await _builder.CreateUserAsync("NoDiscussionsUser");

        // Act
        var count = await _repository.GetDiscussionCountByAuthorAsync(user.PublicId);

        // Assert
        await Assert.That(count).IsEqualTo(0);
    }

    [Test]
    public async Task GetDiscussionCountByAuthorAsync_OnlyCountsOwnDiscussions()
    {
        // Arrange
        var user1 = await _builder.CreateUserAsync("User1");
        var user2 = await _builder.CreateUserAsync("User2");
        var community = await _builder.CreateCommunityAsync();
        var hub = await _builder.CreateHubAsync(community.Id);
        var space = await _builder.CreateSpaceAsync(hub.Id);

        await _builder.CreateDiscussionAsync(space.Id, user1.Id, "User1 Discussion 1");
        await _builder.CreateDiscussionAsync(space.Id, user1.Id, "User1 Discussion 2");
        await _builder.CreateDiscussionAsync(space.Id, user2.Id, "User2 Discussion 1");

        // Act
        var count1 = await _repository.GetDiscussionCountByAuthorAsync(user1.PublicId);
        var count2 = await _repository.GetDiscussionCountByAuthorAsync(user2.PublicId);

        // Assert
        await Assert.That(count1).IsEqualTo(2);
        await Assert.That(count2).IsEqualTo(1);
    }

    #endregion

    #region GetPostCountByAuthorAsync Tests

    [Test]
    public async Task GetPostCountByAuthorAsync_UserWithPosts_ReturnsCorrectCount()
    {
        // Arrange
        var (user, community, hub, space, discussion, firstPost) = await _builder.CreateFullHierarchyAsync();
        await _builder.CreatePostAsync(discussion.Id, user.Id, "Reply 1");
        await _builder.CreatePostAsync(discussion.Id, user.Id, "Reply 2");

        // Act: includes both the first post and replies
        var count = await _repository.GetPostCountByAuthorAsync(user.PublicId);

        // Assert: 1 first post + 2 replies = 3 total posts
        await Assert.That(count).IsEqualTo(3);
    }

    [Test]
    public async Task GetPostCountByAuthorAsync_UserWithNoPosts_ReturnsZero()
    {
        // Arrange
        var user = await _builder.CreateUserAsync("NoPostsUser");

        // Act
        var count = await _repository.GetPostCountByAuthorAsync(user.PublicId);

        // Assert
        await Assert.That(count).IsEqualTo(0);
    }

    #endregion

    #region GetDiscussionsBySpaceAsync Tests

    [Test]
    public async Task GetDiscussionsBySpaceAsync_ReturnsDiscussionsInSpace()
    {
        // Arrange
        var (user, community, hub, space, discussion, firstPost) = await _builder.CreateFullHierarchyAsync();
        var discussion2 = await _builder.CreateDiscussionAsync(space.Id, user.Id, "Second Discussion");
        await _builder.CreatePostAsync(discussion2.Id, user.Id, isFirstPost: true);

        // Act
        var result = await _repository.GetDiscussionsBySpaceAsync(space.PublicId);

        // Assert
        await Assert.That(result.Items.Count()).IsEqualTo(2);
        await Assert.That(result.HasMoreItems).IsFalse();
    }

    [Test]
    public async Task GetDiscussionsBySpaceAsync_ExcludesDeletedDiscussions()
    {
        // Arrange
        var (user, community, hub, space, discussion, firstPost) = await _builder.CreateFullHierarchyAsync();

        var deletedDiscussion = await _builder.CreateDiscussionAsync(space.Id, user.Id, "Deleted Discussion");
        deletedDiscussion.IsDeleted = true;
        deletedDiscussion.DeletedAt = DateTime.UtcNow;
        await _db.Context.SaveChangesAsync();

        // Act
        var result = await _repository.GetDiscussionsBySpaceAsync(space.PublicId);

        // Assert: only the non-deleted discussion should appear
        await Assert.That(result.Items.Count()).IsEqualTo(1);
        await Assert.That(result.Items.First().PublicId).IsEqualTo(discussion.PublicId);
    }

    [Test]
    public async Task GetDiscussionsBySpaceAsync_RespectsPageSize()
    {
        // Arrange
        var user = await _builder.CreateUserAsync();
        var community = await _builder.CreateCommunityAsync();
        var hub = await _builder.CreateHubAsync(community.Id);
        var space = await _builder.CreateSpaceAsync(hub.Id);

        for (var i = 0; i < 5; i++)
        {
            var disc = await _builder.CreateDiscussionAsync(space.Id, user.Id, $"Discussion {i}");
            disc.LastActivityAt = DateTime.UtcNow.AddMinutes(-i); // Ensure ordering
            await _db.Context.SaveChangesAsync();
        }

        // Act: request page size of 3
        var result = await _repository.GetDiscussionsBySpaceAsync(space.PublicId, offset: 0, pageSize: 3);

        // Assert
        await Assert.That(result.Items.Count()).IsEqualTo(3);
        await Assert.That(result.HasMoreItems).IsTrue();
        await Assert.That(result.PageSize).IsEqualTo(3);
    }

    [Test]
    public async Task GetDiscussionsBySpaceAsync_EmptySpace_ReturnsEmptyResult()
    {
        // Arrange
        var community = await _builder.CreateCommunityAsync();
        var hub = await _builder.CreateHubAsync(community.Id);
        var space = await _builder.CreateSpaceAsync(hub.Id);

        // Act
        var result = await _repository.GetDiscussionsBySpaceAsync(space.PublicId);

        // Assert
        await Assert.That(result.Items.Count()).IsEqualTo(0);
        await Assert.That(result.HasMoreItems).IsFalse();
    }

    [Test]
    public async Task GetDiscussionsBySpaceAsync_PinnedDiscussionsAppearFirst()
    {
        // Arrange
        var user = await _builder.CreateUserAsync();
        var community = await _builder.CreateCommunityAsync();
        var hub = await _builder.CreateHubAsync(community.Id);
        var space = await _builder.CreateSpaceAsync(hub.Id);

        var normalDiscussion = await _builder.CreateDiscussionAsync(space.Id, user.Id, "Normal Discussion");
        normalDiscussion.LastActivityAt = DateTime.UtcNow;
        await _db.Context.SaveChangesAsync();

        var pinnedDiscussion = await _builder.CreateDiscussionAsync(space.Id, user.Id, "Pinned Discussion");
        pinnedDiscussion.IsPinned = true;
        pinnedDiscussion.LastActivityAt = DateTime.UtcNow.AddHours(-1); // Older activity
        await _db.Context.SaveChangesAsync();

        // Act
        var result = await _repository.GetDiscussionsBySpaceAsync(space.PublicId);

        // Assert: pinned should come first despite older activity
        var items = result.Items.ToList();
        await Assert.That(items.Count).IsEqualTo(2);
        await Assert.That(items[0].Title).IsEqualTo("Pinned Discussion");
        await Assert.That(items[0].IsPinned).IsTrue();
        await Assert.That(items[1].Title).IsEqualTo("Normal Discussion");
    }

    [Test]
    public async Task GetDiscussionsBySpaceAsync_TypeFilter_ReturnsOnlyMatchingType()
    {
        // Arrange
        var user = await _builder.CreateUserAsync();
        var community = await _builder.CreateCommunityAsync();
        var hub = await _builder.CreateHubAsync(community.Id);
        var space = await _builder.CreateSpaceAsync(hub.Id);

        var standard = await _builder.CreateDiscussionAsync(space.Id, user.Id, "Standard Discussion");
        await _builder.CreatePostAsync(standard.Id, user.Id, isFirstPost: true);

        var question = await _builder.CreateDiscussionAsync(space.Id, user.Id, "Question Discussion");
        question.Type = 1; // Question
        await _db.Context.SaveChangesAsync();
        await _builder.CreatePostAsync(question.Id, user.Id, isFirstPost: true);

        var poll = await _builder.CreateDiscussionAsync(space.Id, user.Id, "Poll Discussion");
        poll.Type = 2; // Poll
        await _db.Context.SaveChangesAsync();
        await _builder.CreatePostAsync(poll.Id, user.Id, isFirstPost: true);

        // Act: filter by Question type
        var result = await _repository.GetDiscussionsBySpaceAsync(space.PublicId, typeFilter: 1);

        // Assert
        await Assert.That(result.Items.Count()).IsEqualTo(1);
        await Assert.That(result.Items.First().Title).IsEqualTo("Question Discussion");
        await Assert.That(result.Items.First().Type).IsEqualTo(1);
    }

    [Test]
    public async Task GetDiscussionsBySpaceAsync_NullTypeFilter_ReturnsAllTypes()
    {
        // Arrange
        var user = await _builder.CreateUserAsync();
        var community = await _builder.CreateCommunityAsync();
        var hub = await _builder.CreateHubAsync(community.Id);
        var space = await _builder.CreateSpaceAsync(hub.Id);

        var standard = await _builder.CreateDiscussionAsync(space.Id, user.Id, "Standard");
        await _builder.CreatePostAsync(standard.Id, user.Id, isFirstPost: true);

        var question = await _builder.CreateDiscussionAsync(space.Id, user.Id, "Question");
        question.Type = 1;
        await _db.Context.SaveChangesAsync();
        await _builder.CreatePostAsync(question.Id, user.Id, isFirstPost: true);

        // Act: no type filter
        var result = await _repository.GetDiscussionsBySpaceAsync(space.PublicId, typeFilter: null);

        // Assert
        await Assert.That(result.Items.Count()).IsEqualTo(2);
    }

    #endregion

    #region GetHubsAsync Tests

    [Test]
    public async Task GetHubsAsync_ReturnsHubsWithStats()
    {
        // Arrange
        var (user, community, hub, space, discussion, firstPost) = await _builder.CreateFullHierarchyAsync();
        await _builder.CreatePostAsync(discussion.Id, user.Id, "Reply");

        // Act
        var result = await _repository.GetHubsAsync();

        // Assert
        await Assert.That(result.Items.Count()).IsEqualTo(1);
        var item = result.Items.First();
        await Assert.That(item.PublicId).IsEqualTo(hub.PublicId);
        await Assert.That(item.SpaceCount).IsEqualTo(1);
        await Assert.That(item.DiscussionCount).IsEqualTo(1);
        await Assert.That(item.ReplyCount).IsEqualTo(1); // Only non-first posts
    }

    [Test]
    public async Task GetHubsAsync_EmptyDatabase_ReturnsEmptyResult()
    {
        // Act
        var result = await _repository.GetHubsAsync();

        // Assert
        await Assert.That(result.Items.Count()).IsEqualTo(0);
    }

    [Test]
    public async Task GetHubsAsync_RespectsPageSize()
    {
        // Arrange
        var community = await _builder.CreateCommunityAsync();
        for (var i = 0; i < 5; i++)
        {
            await _builder.CreateHubAsync(community.Id, $"Hub {i:D2}");
        }

        // Act: request page size of 3
        var result = await _repository.GetHubsAsync(offset: 0, pageSize: 3);

        // Assert
        await Assert.That(result.Items.Count()).IsEqualTo(3);
        await Assert.That(result.HasMoreItems).IsTrue();
    }

    #endregion

    #region GetSpacesByHubAsync Tests

    [Test]
    public async Task GetSpacesByHubAsync_ReturnsSpacesWithStats()
    {
        // Arrange
        var (user, community, hub, space, discussion, firstPost) = await _builder.CreateFullHierarchyAsync();
        await _builder.CreatePostAsync(discussion.Id, user.Id, "Reply");

        // Act
        var result = await _repository.GetSpacesByHubAsync(hub.PublicId);

        // Assert
        await Assert.That(result.Items.Count()).IsEqualTo(1);
        var item = result.Items.First();
        await Assert.That(item.PublicId).IsEqualTo(space.PublicId);
        await Assert.That(item.DiscussionCount).IsEqualTo(1);
        await Assert.That(item.ReplyCount).IsEqualTo(1);
    }

    [Test]
    public async Task GetSpacesByHubAsync_OnlyReturnsSpacesInSpecifiedHub()
    {
        // Arrange
        var community = await _builder.CreateCommunityAsync();
        var hub1 = await _builder.CreateHubAsync(community.Id, "Hub 1");
        var hub2 = await _builder.CreateHubAsync(community.Id, "Hub 2");

        await _builder.CreateSpaceAsync(hub1.Id, "Space in Hub 1");
        await _builder.CreateSpaceAsync(hub2.Id, "Space in Hub 2");
        await _builder.CreateSpaceAsync(hub2.Id, "Another Space in Hub 2");

        // Act
        var result = await _repository.GetSpacesByHubAsync(hub1.PublicId);

        // Assert
        await Assert.That(result.Items.Count()).IsEqualTo(1);
        await Assert.That(result.Items.First().Name).IsEqualTo("Space in Hub 1");
    }

    #endregion

    #region GetSitemapDiscussionsAsync Tests

    [Test]
    public async Task GetSitemapDiscussionsAsync_ReturnsAllNonDeletedDiscussions()
    {
        // Arrange
        var (user, community, hub, space, discussion, firstPost) = await _builder.CreateFullHierarchyAsync();
        var discussion2 = await _builder.CreateDiscussionAsync(space.Id, user.Id, "Second Discussion");

        var deletedDiscussion = await _builder.CreateDiscussionAsync(space.Id, user.Id, "Deleted Discussion");
        deletedDiscussion.IsDeleted = true;
        deletedDiscussion.DeletedAt = DateTime.UtcNow;
        await _db.Context.SaveChangesAsync();

        // Act
        var (sitemapItems, totalCount) = await _repository.GetSitemapDiscussionsAsync(1, 100);

        // Assert: deleted discussion should be excluded
        await Assert.That(sitemapItems.Count).IsEqualTo(2);
        await Assert.That(totalCount).IsEqualTo(2);
    }

    [Test]
    public async Task GetSitemapDiscussionsAsync_EmptyDatabase_ReturnsEmptyList()
    {
        // Act
        var (sitemapItems, totalCount) = await _repository.GetSitemapDiscussionsAsync(1, 100);

        // Assert
        await Assert.That(sitemapItems.Count).IsEqualTo(0);
        await Assert.That(totalCount).IsEqualTo(0);
    }

    [Test]
    public async Task GetSitemapDiscussionsAsync_IncludesSlugHierarchy()
    {
        // Arrange
        var (user, community, hub, space, discussion, firstPost) = await _builder.CreateFullHierarchyAsync();

        // Act
        var (sitemapItems, totalCount) = await _repository.GetSitemapDiscussionsAsync(1, 100);

        // Assert
        await Assert.That(sitemapItems.Count).IsEqualTo(1);
        await Assert.That(totalCount).IsEqualTo(1);
        var item = sitemapItems[0];
        await Assert.That(item.PublicId).IsEqualTo(discussion.PublicId);
        await Assert.That(item.Slug).IsEqualTo(discussion.Slug);
        await Assert.That(item.HubSlug).IsEqualTo(hub.Slug);
        await Assert.That(item.SpaceSlug).IsEqualTo(space.Slug);
        await Assert.That(item.CommunitySlug).IsEqualTo(community.Slug);
    }

    [Test]
    public async Task GetSitemapDiscussionsAsync_RespectsPagination()
    {
        // Arrange
        var (user, community, hub, space, discussion, firstPost) = await _builder.CreateFullHierarchyAsync();
        await _builder.CreateDiscussionAsync(space.Id, user.Id, "Second Discussion");
        await _builder.CreateDiscussionAsync(space.Id, user.Id, "Third Discussion");

        // Act - page 1 with pageSize 2
        var (page1Items, totalCount) = await _repository.GetSitemapDiscussionsAsync(1, 2);
        var (page2Items, _) = await _repository.GetSitemapDiscussionsAsync(2, 2);

        // Assert
        await Assert.That(totalCount).IsEqualTo(3);
        await Assert.That(page1Items.Count).IsEqualTo(2);
        await Assert.That(page2Items.Count).IsEqualTo(1);
    }

    #endregion
}
