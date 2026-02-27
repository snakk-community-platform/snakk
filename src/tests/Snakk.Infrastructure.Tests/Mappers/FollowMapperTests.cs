using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.Mappers;
using Snakk.Shared.Enums;

namespace Snakk.Infrastructure.Tests.Mappers;

public class FollowMapperTests
{
    #region FromPersistence Tests - Discussion Follow

    [Test]
    public async Task FromPersistence_DiscussionFollow_MapsCorrectly()
    {
        var userPublicId = Guid.NewGuid().ToString();
        var discussionPublicId = Guid.NewGuid().ToString();

        var entity = new FollowDatabaseEntity
        {
            PublicId = "follow_disc",
            TargetTypeId = (int)FollowTargetTypeEnum.Discussion,
            LevelId = (int)FollowLevelEnum.DiscussionsAndPosts,
            CreatedAt = new DateTime(2024, 6, 15, 10, 0, 0, DateTimeKind.Utc),
            User = new UserDatabaseEntity
            {
                PublicId = userPublicId,
                DisplayName = "User",
                Email = "u@e.com",
                CreatedAt = DateTime.UtcNow
            },
            Discussion = new DiscussionDatabaseEntity
            {
                PublicId = discussionPublicId,
                Title = "Discussion",
                Slug = "discussion",
                CreatedAt = DateTime.UtcNow
            },
            Space = null,
            FollowedUser = null
        };

        var follow = entity.FromPersistence();

        await Assert.That(follow).IsNotNull();
        await Assert.That(follow.PublicId.Value).IsEqualTo("follow_disc");
        await Assert.That(follow.UserId.Value).IsEqualTo(userPublicId);
        await Assert.That(follow.TargetType).IsEqualTo(FollowTargetType.Discussion);
        await Assert.That(follow.DiscussionId).IsNotNull();
        await Assert.That(follow.DiscussionId!.Value).IsEqualTo(discussionPublicId);
        await Assert.That((object?)follow.SpaceId).IsNull();
        await Assert.That((object?)follow.FollowedUserId).IsNull();
        await Assert.That(follow.Level).IsEqualTo(FollowLevel.DiscussionsAndPosts);
    }

    #endregion

    #region FromPersistence Tests - Space Follow

    [Test]
    public async Task FromPersistence_SpaceFollow_MapsCorrectly()
    {
        var userPublicId = Guid.NewGuid().ToString();
        var spacePublicId = Guid.NewGuid().ToString();

        var entity = new FollowDatabaseEntity
        {
            PublicId = "follow_space",
            TargetTypeId = (int)FollowTargetTypeEnum.Space,
            LevelId = (int)FollowLevelEnum.DiscussionsOnly,
            CreatedAt = DateTime.UtcNow,
            User = new UserDatabaseEntity
            {
                PublicId = userPublicId,
                DisplayName = "User",
                Email = "u@e.com",
                CreatedAt = DateTime.UtcNow
            },
            Space = new SpaceDatabaseEntity
            {
                PublicId = spacePublicId,
                Name = "Space",
                Slug = "space",
                CreatedAt = DateTime.UtcNow,
                HubId = 1,
                Hub = new HubDatabaseEntity { PublicId = "h1", Name = "Hub", Slug = "hub", CreatedAt = DateTime.UtcNow, CommunityId = 1 }
            },
            Discussion = null,
            FollowedUser = null
        };

        var follow = entity.FromPersistence();

        await Assert.That(follow.TargetType).IsEqualTo(FollowTargetType.Space);
        await Assert.That(follow.SpaceId).IsNotNull();
        await Assert.That(follow.SpaceId!.Value).IsEqualTo(spacePublicId);
        await Assert.That((object?)follow.DiscussionId).IsNull();
        await Assert.That((object?)follow.FollowedUserId).IsNull();
        await Assert.That(follow.Level).IsEqualTo(FollowLevel.DiscussionsOnly);
    }

    #endregion

    #region FromPersistence Tests - User Follow

    [Test]
    public async Task FromPersistence_UserFollow_MapsCorrectly()
    {
        var followerPublicId = Guid.NewGuid().ToString();
        var followedPublicId = Guid.NewGuid().ToString();

        var entity = new FollowDatabaseEntity
        {
            PublicId = "follow_user",
            TargetTypeId = (int)FollowTargetTypeEnum.User,
            LevelId = (int)FollowLevelEnum.DiscussionsAndPosts,
            CreatedAt = DateTime.UtcNow,
            User = new UserDatabaseEntity
            {
                PublicId = followerPublicId,
                DisplayName = "Follower",
                Email = "f@e.com",
                CreatedAt = DateTime.UtcNow
            },
            FollowedUser = new UserDatabaseEntity
            {
                PublicId = followedPublicId,
                DisplayName = "Followed",
                Email = "followed@e.com",
                CreatedAt = DateTime.UtcNow
            },
            Discussion = null,
            Space = null
        };

        var follow = entity.FromPersistence();

        await Assert.That(follow.TargetType).IsEqualTo(FollowTargetType.User);
        await Assert.That(follow.FollowedUserId).IsNotNull();
        await Assert.That(follow.FollowedUserId!.Value).IsEqualTo(followedPublicId);
        await Assert.That((object?)follow.DiscussionId).IsNull();
        await Assert.That((object?)follow.SpaceId).IsNull();
    }

    #endregion

    #region ToPersistence Tests

    [Test]
    public async Task ToPersistence_DiscussionFollow_MapsCorrectly()
    {
        var follow = Follow.Rehydrate(
            FollowId.From("f1"),
            UserId.From("u1"),
            FollowTargetType.Discussion,
            DiscussionId.From("d1"),
            null,
            null,
            FollowLevel.DiscussionsAndPosts,
            DateTime.UtcNow);

        var entity = follow.ToPersistence();

        await Assert.That(entity).IsNotNull();
        await Assert.That(entity.PublicId).IsEqualTo("f1");
        await Assert.That(entity.TargetTypeId).IsEqualTo((int)FollowTargetTypeEnum.Discussion);
        await Assert.That(entity.LevelId).IsEqualTo((int)FollowLevelEnum.DiscussionsAndPosts);
    }

    [Test]
    public async Task ToPersistence_SpaceFollow_MapsCorrectly()
    {
        var follow = Follow.Rehydrate(
            FollowId.From("f2"),
            UserId.From("u1"),
            FollowTargetType.Space,
            null,
            SpaceId.From("s1"),
            null,
            FollowLevel.DiscussionsOnly,
            DateTime.UtcNow);

        var entity = follow.ToPersistence();

        await Assert.That(entity.TargetTypeId).IsEqualTo((int)FollowTargetTypeEnum.Space);
        await Assert.That(entity.LevelId).IsEqualTo((int)FollowLevelEnum.DiscussionsOnly);
    }

    [Test]
    public async Task ToPersistence_UserFollow_MapsCorrectly()
    {
        var follow = Follow.Rehydrate(
            FollowId.From("f3"),
            UserId.From("u1"),
            FollowTargetType.User,
            null,
            null,
            UserId.From("u2"),
            FollowLevel.DiscussionsAndPosts,
            DateTime.UtcNow);

        var entity = follow.ToPersistence();

        await Assert.That(entity.TargetTypeId).IsEqualTo((int)FollowTargetTypeEnum.User);
    }

    [Test]
    public async Task ToPersistence_PreservesTimestamp()
    {
        var specificTime = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);
        var follow = Follow.Rehydrate(
            FollowId.From("f4"),
            UserId.From("u1"),
            FollowTargetType.Discussion,
            DiscussionId.From("d1"),
            null, null,
            FollowLevel.DiscussionsAndPosts,
            specificTime);

        var entity = follow.ToPersistence();

        await Assert.That(entity.CreatedAt).IsEqualTo(specificTime);
    }

    #endregion
}
