using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.Database.Repositories;
using Snakk.Infrastructure.Tests.Helpers;
using Snakk.Shared.Enums;

namespace Snakk.Infrastructure.Tests.Repositories;

public class StatsRepositoryIntegrationTests : IDisposable
{
    private readonly SqliteTestDatabase _db;
    private readonly TestDataBuilder _builder;
    private readonly StatsRepository _repository;

    public StatsRepositoryIntegrationTests()
    {
        _db = new SqliteTestDatabase();
        _builder = new TestDataBuilder(_db.Context);
        _repository = new StatsRepository(_db.Context);
    }

    public void Dispose() => _db.Dispose();

    #region GetPlatformStatsAsync Tests

    [Test]
    public async Task GetPlatformStatsAsync_WithData_ReturnsCorrectTotals()
    {
        // Arrange: create a full hierarchy with 1 hub, 1 space, 1 discussion, 1 first post
        var (user, community, hub, space, discussion, firstPost) = await _builder.CreateFullHierarchyAsync();

        // Add a reply (non-first post)
        await _builder.CreatePostAsync(discussion.Id, user.Id, "Reply 1");
        await _builder.CreatePostAsync(discussion.Id, user.Id, "Reply 2");

        // Add a second space with its own discussion
        var space2 = await _builder.CreateSpaceAsync(hub.Id);
        var discussion2 = await _builder.CreateDiscussionAsync(space2.Id, user.Id);
        await _builder.CreatePostAsync(discussion2.Id, user.Id, isFirstPost: true);
        await _builder.CreatePostAsync(discussion2.Id, user.Id, "Another reply");

        // Act
        var stats = await _repository.GetPlatformStatsAsync();

        // Assert: 1 hub, 2 spaces, 2 discussions, 3 replies (non-first posts)
        await Assert.That(stats.HubCount).IsEqualTo(1);
        await Assert.That(stats.SpaceCount).IsEqualTo(2);
        await Assert.That(stats.DiscussionCount).IsEqualTo(2);
        await Assert.That(stats.ReplyCount).IsEqualTo(3);
    }

    [Test]
    public async Task GetPlatformStatsAsync_EmptyDatabase_ReturnsZeroCounts()
    {
        // Act
        var stats = await _repository.GetPlatformStatsAsync();

        // Assert
        await Assert.That(stats.HubCount).IsEqualTo(0);
        await Assert.That(stats.SpaceCount).IsEqualTo(0);
        await Assert.That(stats.DiscussionCount).IsEqualTo(0);
        await Assert.That(stats.ReplyCount).IsEqualTo(0);
    }

    #endregion

    #region GetHubStatsAsync Tests

    [Test]
    public async Task GetHubStatsAsync_ExistingHub_ReturnsCorrectCounts()
    {
        // Arrange
        var (user, community, hub, space, discussion, firstPost) = await _builder.CreateFullHierarchyAsync();
        await _builder.CreatePostAsync(discussion.Id, user.Id, "Reply in discussion");

        var space2 = await _builder.CreateSpaceAsync(hub.Id, "Second Space");
        var discussion2 = await _builder.CreateDiscussionAsync(space2.Id, user.Id, "Second Discussion");
        await _builder.CreatePostAsync(discussion2.Id, user.Id, isFirstPost: true);

        // Act
        var stats = await _repository.GetHubStatsAsync(hub.PublicId);

        // Assert
        await Assert.That(stats).IsNotNull();
        await Assert.That(stats!.PublicId).IsEqualTo(hub.PublicId);
        await Assert.That(stats.SpaceCount).IsEqualTo(2);
        await Assert.That(stats.DiscussionCount).IsEqualTo(2);
        await Assert.That(stats.ReplyCount).IsEqualTo(1); // Only non-first posts
    }

    [Test]
    public async Task GetHubStatsAsync_NonExistentHub_ReturnsNull()
    {
        // Act
        var stats = await _repository.GetHubStatsAsync("nonexistent_hub_id");

        // Assert
        await Assert.That(stats).IsNull();
    }

    #endregion

    #region GetSpaceStatsAsync Tests

    [Test]
    public async Task GetSpaceStatsAsync_ExistingSpace_ReturnsCorrectCounts()
    {
        // Arrange
        var (user, community, hub, space, discussion, firstPost) = await _builder.CreateFullHierarchyAsync();
        await _builder.CreatePostAsync(discussion.Id, user.Id, "Reply 1");
        await _builder.CreatePostAsync(discussion.Id, user.Id, "Reply 2");

        var discussion2 = await _builder.CreateDiscussionAsync(space.Id, user.Id, "Another Discussion");
        await _builder.CreatePostAsync(discussion2.Id, user.Id, isFirstPost: true);

        // Act
        var stats = await _repository.GetSpaceStatsAsync(space.PublicId);

        // Assert
        await Assert.That(stats).IsNotNull();
        await Assert.That(stats!.PublicId).IsEqualTo(space.PublicId);
        await Assert.That(stats.DiscussionCount).IsEqualTo(2);
        await Assert.That(stats.ReplyCount).IsEqualTo(2); // Only non-first posts
        await Assert.That(stats.FollowerCount).IsEqualTo(0);
    }

    [Test]
    public async Task GetSpaceStatsAsync_NonExistentSpace_ReturnsNull()
    {
        // Act
        var stats = await _repository.GetSpaceStatsAsync("nonexistent_space_id");

        // Assert
        await Assert.That(stats).IsNull();
    }

    #endregion

    #region GetCommunityStatsAsync Tests

    [Test]
    public async Task GetCommunityStatsAsync_ExistingCommunity_ReturnsCorrectCounts()
    {
        // Arrange: full hierarchy gives us 1 community, 1 hub, 1 space, 1 discussion, 1 first post
        var (user, community, hub, space, discussion, firstPost) = await _builder.CreateFullHierarchyAsync();
        await _builder.CreatePostAsync(discussion.Id, user.Id, "Reply");

        // Add a second hub with its own space and discussion
        var hub2 = await _builder.CreateHubAsync(community.Id, "Hub 2");
        var space2 = await _builder.CreateSpaceAsync(hub2.Id, "Space in Hub 2");
        var discussion2 = await _builder.CreateDiscussionAsync(space2.Id, user.Id, "Discussion in Hub 2");
        await _builder.CreatePostAsync(discussion2.Id, user.Id, isFirstPost: true);

        // Act
        var stats = await _repository.GetCommunityStatsAsync(community.PublicId);

        // Assert
        await Assert.That(stats).IsNotNull();
        await Assert.That(stats!.PublicId).IsEqualTo(community.PublicId);
        await Assert.That(stats.HubCount).IsEqualTo(2);
        await Assert.That(stats.SpaceCount).IsEqualTo(2);
        await Assert.That(stats.DiscussionCount).IsEqualTo(2);
        await Assert.That(stats.ReplyCount).IsEqualTo(1); // Only non-first posts
    }

    [Test]
    public async Task GetCommunityStatsAsync_NonExistentCommunity_ReturnsNull()
    {
        // Act
        var stats = await _repository.GetCommunityStatsAsync("nonexistent_community_id");

        // Assert
        await Assert.That(stats).IsNull();
    }

    #endregion

    #region GetUserStatsAsync Tests

    [Test]
    public async Task GetUserStatsAsync_ExistingUser_ReturnsCorrectCounts()
    {
        // Arrange
        var (user, community, hub, space, discussion, firstPost) = await _builder.CreateFullHierarchyAsync();
        await _builder.CreatePostAsync(discussion.Id, user.Id, "Reply by user");

        // Create a second discussion by the same user
        var discussion2 = await _builder.CreateDiscussionAsync(space.Id, user.Id, "Second Discussion");
        await _builder.CreatePostAsync(discussion2.Id, user.Id, isFirstPost: true);

        // Act
        var stats = await _repository.GetUserStatsAsync(user.PublicId);

        // Assert
        await Assert.That(stats).IsNotNull();
        await Assert.That(stats!.PublicId).IsEqualTo(user.PublicId);
        await Assert.That(stats.DiscussionCount).IsEqualTo(2);
        await Assert.That(stats.ReplyCount).IsEqualTo(1); // Only non-first posts
        await Assert.That(stats.FollowerCount).IsEqualTo(0);
    }

    [Test]
    public async Task GetUserStatsAsync_NonExistentUser_ReturnsNull()
    {
        // Act
        var stats = await _repository.GetUserStatsAsync("nonexistent_user_id");

        // Assert
        await Assert.That(stats).IsNull();
    }

    [Test]
    public async Task GetUserStatsAsync_UserWithFollowers_IncludesFollowerCount()
    {
        // Arrange
        var user = await _builder.CreateUserAsync("TargetUser");
        var follower = await _builder.CreateUserAsync("Follower");

        _db.Context.Follows.Add(new FollowDatabaseEntity
        {
            PublicId = $"follow_{Guid.NewGuid():N}",
            UserId = follower.Id,
            FollowedUserId = user.Id,
            TargetTypeId = (int)FollowTargetTypeEnum.User,
            CreatedAt = DateTime.UtcNow
        });
        user.FollowerCount++;
        await _db.Context.SaveChangesAsync();

        // Act
        var stats = await _repository.GetUserStatsAsync(user.PublicId);

        // Assert
        await Assert.That(stats).IsNotNull();
        await Assert.That(stats!.FollowerCount).IsEqualTo(1);
    }

    #endregion

    #region GetDiscussionStatsAsync Tests

    [Test]
    public async Task GetDiscussionStatsAsync_ExistingDiscussion_ReturnsCorrectCounts()
    {
        // Arrange
        var (user, community, hub, space, discussion, firstPost) = await _builder.CreateFullHierarchyAsync();
        await _builder.CreatePostAsync(discussion.Id, user.Id, "Reply 1");
        await _builder.CreatePostAsync(discussion.Id, user.Id, "Reply 2");
        await _builder.CreatePostAsync(discussion.Id, user.Id, "Reply 3");

        // Act
        var stats = await _repository.GetDiscussionStatsAsync(discussion.PublicId);

        // Assert
        await Assert.That(stats).IsNotNull();
        await Assert.That(stats!.PublicId).IsEqualTo(discussion.PublicId);
        await Assert.That(stats.Title).IsEqualTo(discussion.Title);
        await Assert.That(stats.ReplyCount).IsEqualTo(3);
        await Assert.That(stats.FollowerCount).IsEqualTo(0);
    }

    [Test]
    public async Task GetDiscussionStatsAsync_NonExistentDiscussion_ReturnsNull()
    {
        // Act
        var stats = await _repository.GetDiscussionStatsAsync("nonexistent_discussion_id");

        // Assert
        await Assert.That(stats).IsNull();
    }

    [Test]
    public async Task GetDiscussionStatsAsync_WithFollowers_IncludesFollowerCount()
    {
        // Arrange
        var (user, community, hub, space, discussion, firstPost) = await _builder.CreateFullHierarchyAsync();
        var follower = await _builder.CreateUserAsync("Follower");

        _db.Context.Follows.Add(new FollowDatabaseEntity
        {
            PublicId = $"follow_{Guid.NewGuid():N}",
            UserId = follower.Id,
            DiscussionId = discussion.Id,
            TargetTypeId = (int)FollowTargetTypeEnum.Discussion,
            CreatedAt = DateTime.UtcNow
        });
        discussion.FollowerCount++;
        await _db.Context.SaveChangesAsync();

        // Act
        var stats = await _repository.GetDiscussionStatsAsync(discussion.PublicId);

        // Assert
        await Assert.That(stats).IsNotNull();
        await Assert.That(stats!.FollowerCount).IsEqualTo(1);
    }

    #endregion

    #region GetTopActiveSpacesSinceAsync Tests

    [Test]
    public async Task GetTopActiveSpacesSinceAsync_WithRecentPosts_ReturnsActiveSpaces()
    {
        // Arrange
        var (user, community, hub, space, discussion, firstPost) = await _builder.CreateFullHierarchyAsync();

        // Add posts created today (CreatePostAsync sets CreatedAt to UtcNow)
        await _builder.CreatePostAsync(discussion.Id, user.Id, "Today post 1");
        await _builder.CreatePostAsync(discussion.Id, user.Id, "Today post 2");

        // Act
        var topSpaces = await _repository.GetTopActiveSpacesSinceAsync(DateTime.UtcNow.AddHours(-24));

        // Assert: the first post + 2 additional posts = 3 posts today in that space
        await Assert.That(topSpaces.Count).IsEqualTo(1);
        await Assert.That(topSpaces[0].PublicId).IsEqualTo(space.PublicId);
        await Assert.That(topSpaces[0].PostCountToday).IsEqualTo(3);
    }

    [Test]
    public async Task GetTopActiveSpacesSinceAsync_EmptyDatabase_ReturnsEmptyList()
    {
        // Act
        var topSpaces = await _repository.GetTopActiveSpacesSinceAsync(DateTime.UtcNow.AddHours(-24));

        // Assert
        await Assert.That(topSpaces.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetTopActiveSpacesSinceAsync_RespectsLimitParameter()
    {
        // Arrange: create 3 spaces with different post counts
        var user = await _builder.CreateUserAsync();
        var community = await _builder.CreateCommunityAsync();
        var hub = await _builder.CreateHubAsync(community.Id);

        var space1 = await _builder.CreateSpaceAsync(hub.Id, "Space 1");
        var disc1 = await _builder.CreateDiscussionAsync(space1.Id, user.Id, "D1");
        await _builder.CreatePostAsync(disc1.Id, user.Id, "Post 1", isFirstPost: true);

        var space2 = await _builder.CreateSpaceAsync(hub.Id, "Space 2");
        var disc2 = await _builder.CreateDiscussionAsync(space2.Id, user.Id, "D2");
        await _builder.CreatePostAsync(disc2.Id, user.Id, "Post 1", isFirstPost: true);
        await _builder.CreatePostAsync(disc2.Id, user.Id, "Post 2");

        var space3 = await _builder.CreateSpaceAsync(hub.Id, "Space 3");
        var disc3 = await _builder.CreateDiscussionAsync(space3.Id, user.Id, "D3");
        await _builder.CreatePostAsync(disc3.Id, user.Id, "Post 1", isFirstPost: true);
        await _builder.CreatePostAsync(disc3.Id, user.Id, "Post 2");
        await _builder.CreatePostAsync(disc3.Id, user.Id, "Post 3");

        // Act: request only top 2
        var topSpaces = await _repository.GetTopActiveSpacesSinceAsync(DateTime.UtcNow.AddHours(-24), limit: 2);

        // Assert
        await Assert.That(topSpaces.Count).IsEqualTo(2);
        // Ordered by post count descending: space3 (3 posts), space2 (2 posts)
        await Assert.That(topSpaces[0].PublicId).IsEqualTo(space3.PublicId);
        await Assert.That(topSpaces[1].PublicId).IsEqualTo(space2.PublicId);
    }

    #endregion
}
