using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.Database.Repositories;
using Snakk.Infrastructure.Tests.Helpers;
using Snakk.Shared.Enums;

namespace Snakk.Infrastructure.Tests.Repositories;

public class FollowRepositoryIntegrationTests : IDisposable
{
    private readonly SqliteTestDatabase _db;
    private readonly TestDataBuilder _builder;
    private readonly FollowDatabaseRepository _repository;

    public FollowRepositoryIntegrationTests()
    {
        _db = new SqliteTestDatabase();
        _builder = new TestDataBuilder(_db.Context);
        _repository = new FollowDatabaseRepository(_db.Context);
    }

    public void Dispose() => _db.Dispose();

    #region Helper Methods

    private async Task<FollowDatabaseEntity> CreateDiscussionFollowAsync(int userId, int discussionId)
    {
        var follow = new FollowDatabaseEntity
        {
            PublicId = $"follow_{Guid.NewGuid():N}",
            UserId = userId,
            TargetTypeId = (int)FollowTargetTypeEnum.Discussion,
            DiscussionId = discussionId,
            LevelId = 1,
            CreatedAt = DateTime.UtcNow
        };
        _db.Context.Follows.Add(follow);
        await _db.Context.SaveChangesAsync();
        return follow;
    }

    private async Task<FollowDatabaseEntity> CreateSpaceFollowAsync(int userId, int spaceId, int levelId = 1)
    {
        var follow = new FollowDatabaseEntity
        {
            PublicId = $"follow_{Guid.NewGuid():N}",
            UserId = userId,
            TargetTypeId = (int)FollowTargetTypeEnum.Space,
            SpaceId = spaceId,
            LevelId = levelId,
            CreatedAt = DateTime.UtcNow
        };
        _db.Context.Follows.Add(follow);
        await _db.Context.SaveChangesAsync();
        return follow;
    }

    private async Task<FollowDatabaseEntity> CreateUserFollowAsync(int userId, int followedUserId)
    {
        var follow = new FollowDatabaseEntity
        {
            PublicId = $"follow_{Guid.NewGuid():N}",
            UserId = userId,
            TargetTypeId = (int)FollowTargetTypeEnum.User,
            FollowedUserId = followedUserId,
            LevelId = 1,
            CreatedAt = DateTime.UtcNow
        };
        _db.Context.Follows.Add(follow);
        await _db.Context.SaveChangesAsync();
        return follow;
    }

    #endregion

    #region GetByUserAndDiscussionAsync Tests

    [Test]
    public async Task GetByUserAndDiscussionAsync_ExistingFollow_ReturnsEntity()
    {
        var (user, _, _, space, discussion, _) = await _builder.CreateFullHierarchyAsync();
        await CreateDiscussionFollowAsync(user.Id, discussion.Id);

        var result = await _repository.GetByUserAndDiscussionAsync(user.Id, discussion.Id);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.UserId).IsEqualTo(user.Id);
        await Assert.That(result.DiscussionId).IsEqualTo(discussion.Id);
        await Assert.That(result.TargetTypeId).IsEqualTo((int)FollowTargetTypeEnum.Discussion);
    }

    [Test]
    public async Task GetByUserAndDiscussionAsync_NoFollow_ReturnsNull()
    {
        var (user, _, _, _, discussion, _) = await _builder.CreateFullHierarchyAsync();

        var result = await _repository.GetByUserAndDiscussionAsync(user.Id, discussion.Id);

        await Assert.That(result).IsNull();
    }

    #endregion

    #region GetByUserAndSpaceAsync Tests

    [Test]
    public async Task GetByUserAndSpaceAsync_ExistingFollow_ReturnsEntity()
    {
        var (user, _, _, space, _, _) = await _builder.CreateFullHierarchyAsync();
        await CreateSpaceFollowAsync(user.Id, space.Id);

        var result = await _repository.GetByUserAndSpaceAsync(user.Id, space.Id);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.UserId).IsEqualTo(user.Id);
        await Assert.That(result.SpaceId).IsEqualTo(space.Id);
        await Assert.That(result.TargetTypeId).IsEqualTo((int)FollowTargetTypeEnum.Space);
    }

    [Test]
    public async Task GetByUserAndSpaceAsync_NoFollow_ReturnsNull()
    {
        var (user, _, _, space, _, _) = await _builder.CreateFullHierarchyAsync();

        var result = await _repository.GetByUserAndSpaceAsync(user.Id, space.Id);

        await Assert.That(result).IsNull();
    }

    #endregion

    #region GetByUserAndFollowedUserAsync Tests

    [Test]
    public async Task GetByUserAndFollowedUserAsync_ExistingFollow_ReturnsEntity()
    {
        var follower = await _builder.CreateUserAsync("Follower");
        var followed = await _builder.CreateUserAsync("Followed");
        await CreateUserFollowAsync(follower.Id, followed.Id);

        var result = await _repository.GetByUserAndFollowedUserAsync(follower.Id, followed.Id);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.UserId).IsEqualTo(follower.Id);
        await Assert.That(result.FollowedUserId).IsEqualTo(followed.Id);
        await Assert.That(result.TargetTypeId).IsEqualTo((int)FollowTargetTypeEnum.User);
    }

    [Test]
    public async Task GetByUserAndFollowedUserAsync_NoFollow_ReturnsNull()
    {
        var follower = await _builder.CreateUserAsync("Follower");
        var followed = await _builder.CreateUserAsync("Followed");

        var result = await _repository.GetByUserAndFollowedUserAsync(follower.Id, followed.Id);

        await Assert.That(result).IsNull();
    }

    #endregion

    #region GetFollowerUserIdsOfDiscussionAsync Tests

    [Test]
    public async Task GetFollowerUserIdsOfDiscussionAsync_MultipleFollowers_ReturnsAllUserIds()
    {
        var (creator, _, _, _, discussion, _) = await _builder.CreateFullHierarchyAsync();
        var user2 = await _builder.CreateUserAsync("User2");
        var user3 = await _builder.CreateUserAsync("User3");

        await CreateDiscussionFollowAsync(creator.Id, discussion.Id);
        await CreateDiscussionFollowAsync(user2.Id, discussion.Id);
        await CreateDiscussionFollowAsync(user3.Id, discussion.Id);

        var result = (await _repository.GetFollowerUserIdsOfDiscussionAsync(discussion.Id)).ToList();

        await Assert.That(result.Count).IsEqualTo(3);
        await Assert.That(result).Contains(creator.Id);
        await Assert.That(result).Contains(user2.Id);
        await Assert.That(result).Contains(user3.Id);
    }

    [Test]
    public async Task GetFollowerUserIdsOfDiscussionAsync_NoFollowers_ReturnsEmpty()
    {
        var (_, _, _, _, discussion, _) = await _builder.CreateFullHierarchyAsync();

        var result = (await _repository.GetFollowerUserIdsOfDiscussionAsync(discussion.Id)).ToList();

        await Assert.That(result.Count).IsEqualTo(0);
    }

    #endregion

    #region IsFollowingDiscussionAsync Tests

    [Test]
    public async Task IsFollowingDiscussionAsync_UserIsFollowing_ReturnsTrue()
    {
        var (user, _, _, _, discussion, _) = await _builder.CreateFullHierarchyAsync();
        await CreateDiscussionFollowAsync(user.Id, discussion.Id);

        var result = await _repository.IsFollowingDiscussionAsync(user.Id, discussion.Id);

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsFollowingDiscussionAsync_UserIsNotFollowing_ReturnsFalse()
    {
        var (user, _, _, _, discussion, _) = await _builder.CreateFullHierarchyAsync();

        var result = await _repository.IsFollowingDiscussionAsync(user.Id, discussion.Id);

        await Assert.That(result).IsFalse();
    }

    #endregion

    #region IsFollowingSpaceAsync Tests

    [Test]
    public async Task IsFollowingSpaceAsync_UserIsFollowing_ReturnsTrue()
    {
        var (user, _, _, space, _, _) = await _builder.CreateFullHierarchyAsync();
        await CreateSpaceFollowAsync(user.Id, space.Id);

        var result = await _repository.IsFollowingSpaceAsync(user.Id, space.Id);

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsFollowingSpaceAsync_UserIsNotFollowing_ReturnsFalse()
    {
        var (user, _, _, space, _, _) = await _builder.CreateFullHierarchyAsync();

        var result = await _repository.IsFollowingSpaceAsync(user.Id, space.Id);

        await Assert.That(result).IsFalse();
    }

    #endregion

    #region IsFollowingUserAsync Tests

    [Test]
    public async Task IsFollowingUserAsync_UserIsFollowing_ReturnsTrue()
    {
        var follower = await _builder.CreateUserAsync("Follower");
        var followed = await _builder.CreateUserAsync("Followed");
        await CreateUserFollowAsync(follower.Id, followed.Id);

        var result = await _repository.IsFollowingUserAsync(follower.Id, followed.Id);

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsFollowingUserAsync_UserIsNotFollowing_ReturnsFalse()
    {
        var follower = await _builder.CreateUserAsync("Follower");
        var followed = await _builder.CreateUserAsync("Followed");

        var result = await _repository.IsFollowingUserAsync(follower.Id, followed.Id);

        await Assert.That(result).IsFalse();
    }

    #endregion

    #region GetFollowedSpacePublicIdsByUserAsync Tests

    [Test]
    public async Task GetFollowedSpacePublicIdsByUserAsync_MultipleSpaces_ReturnsPublicIds()
    {
        var user = await _builder.CreateUserAsync();
        var community = await _builder.CreateCommunityAsync();
        var hub = await _builder.CreateHubAsync(community.Id);
        var space1 = await _builder.CreateSpaceAsync(hub.Id, "Space One");
        var space2 = await _builder.CreateSpaceAsync(hub.Id, "Space Two");

        await CreateSpaceFollowAsync(user.Id, space1.Id);
        await CreateSpaceFollowAsync(user.Id, space2.Id);

        var result = (await _repository.GetFollowedSpacePublicIdsByUserAsync(user.Id)).ToList();

        await Assert.That(result.Count).IsEqualTo(2);
        await Assert.That(result).Contains(space1.PublicId);
        await Assert.That(result).Contains(space2.PublicId);
    }

    [Test]
    public async Task GetFollowedSpacePublicIdsByUserAsync_NoFollows_ReturnsEmpty()
    {
        var user = await _builder.CreateUserAsync();

        var result = (await _repository.GetFollowedSpacePublicIdsByUserAsync(user.Id)).ToList();

        await Assert.That(result.Count).IsEqualTo(0);
    }

    #endregion

    #region GetFollowerCountOfUserAsync Tests

    [Test]
    public async Task GetFollowerCountOfUserAsync_MultipleFollowers_ReturnsCorrectCount()
    {
        var targetUser = await _builder.CreateUserAsync("TargetUser");
        var follower1 = await _builder.CreateUserAsync("Follower1");
        var follower2 = await _builder.CreateUserAsync("Follower2");
        var follower3 = await _builder.CreateUserAsync("Follower3");

        await CreateUserFollowAsync(follower1.Id, targetUser.Id);
        await CreateUserFollowAsync(follower2.Id, targetUser.Id);
        await CreateUserFollowAsync(follower3.Id, targetUser.Id);

        var result = await _repository.GetFollowerCountOfUserAsync(targetUser.Id);

        await Assert.That(result).IsEqualTo(3);
    }

    [Test]
    public async Task GetFollowerCountOfUserAsync_NoFollowers_ReturnsZero()
    {
        var targetUser = await _builder.CreateUserAsync("TargetUser");

        var result = await _repository.GetFollowerCountOfUserAsync(targetUser.Id);

        await Assert.That(result).IsEqualTo(0);
    }

    [Test]
    public async Task GetFollowerCountOfUserAsync_DoesNotCountOtherFollowTypes()
    {
        var (user, _, _, space, discussion, _) = await _builder.CreateFullHierarchyAsync();
        var otherUser = await _builder.CreateUserAsync("OtherUser");

        // Create a discussion follow and a space follow targeting entities related to user,
        // but these should not count as user followers
        await CreateDiscussionFollowAsync(otherUser.Id, discussion.Id);
        await CreateSpaceFollowAsync(otherUser.Id, space.Id);

        var result = await _repository.GetFollowerCountOfUserAsync(user.Id);

        await Assert.That(result).IsEqualTo(0);
    }

    #endregion
}
