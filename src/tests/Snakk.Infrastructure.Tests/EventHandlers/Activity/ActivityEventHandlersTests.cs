using Microsoft.EntityFrameworkCore;
using Moq;
using Snakk.Application.Services;
using Snakk.Domain.Events;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.EventHandlers.Activity;
using Snakk.Shared.Enums;

namespace Snakk.Infrastructure.Tests.EventHandlers.Activity;

public class ActivityEventHandlersTests : IDisposable
{
    private readonly SnakkDbContext _context;
    private readonly Mock<IActivityBroadcaster> _mockBroadcaster;

    public ActivityEventHandlersTests()
    {
        var options = new DbContextOptionsBuilder<SnakkDbContext>()
            .UseInMemoryDatabase(databaseName: $"ActivityHandlerTests_{Guid.NewGuid()}")
            .Options;
        _context = new SnakkDbContext(options);
        _mockBroadcaster = new Mock<IActivityBroadcaster>();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private async Task<(UserDatabaseEntity user, CommunityDatabaseEntity community, HubDatabaseEntity hub, SpaceDatabaseEntity space)> SetupHierarchy()
    {
        var user = new UserDatabaseEntity
        {
            PublicId = "user_activity",
            DisplayName = "ActivityUser",
            Email = "activity@example.com",
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);

        var community = new CommunityDatabaseEntity
        {
            PublicId = "comm_act",
            Name = "Activity Community",
            Slug = "activity-community",
            CreatedAt = DateTime.UtcNow,
            VisibilityId = 1
        };
        _context.Communities.Add(community);
        await _context.SaveChangesAsync();

        var hub = new HubDatabaseEntity
        {
            PublicId = "hub_act",
            Name = "Activity Hub",
            Slug = "activity-hub",
            CommunityId = community.Id,
            CreatedAt = DateTime.UtcNow
        };
        _context.Hubs.Add(hub);
        await _context.SaveChangesAsync();

        var space = new SpaceDatabaseEntity
        {
            PublicId = "space_act",
            Name = "Activity Space",
            Slug = "activity-space",
            HubId = hub.Id,
            CreatedAt = DateTime.UtcNow
        };
        _context.Spaces.Add(space);
        await _context.SaveChangesAsync();

        return (user, community, hub, space);
    }

    #region PostCreatedActivityHandler Tests

    [Test]
    public async Task PostCreatedActivityHandler_BroadcastsPostCreated()
    {
        var (user, community, hub, space) = await SetupHierarchy();
        var discussion = new DiscussionDatabaseEntity
        {
            PublicId = "disc_post_act",
            Title = "Discussion for Post",
            Slug = "disc-post",
            SpaceId = space.Id,
            CreatedByUserId = user.Id,
            CreatedAt = DateTime.UtcNow
        };
        _context.Discussions.Add(discussion);
        await _context.SaveChangesAsync();

        var post = new PostDatabaseEntity
        {
            PublicId = "post_act",
            Content = "Activity post content",
            DiscussionId = discussion.Id,
            CreatedByUserId = user.Id,
            CreatedAt = DateTime.UtcNow,
            LastModifiedAt = DateTime.UtcNow
        };
        _context.Posts.Add(post);
        await _context.SaveChangesAsync();

        var handler = new PostCreatedActivityHandler(_mockBroadcaster.Object, _context);
        var @event = new PostCreatedEvent(
            PostId.From("post_act"),
            DiscussionId.From("disc_post_act"),
            UserId.From("user_activity"));

        await handler.HandleAsync(@event);

        _mockBroadcaster.Verify(b => b.BroadcastPostCreated(
            "user_activity",
            "ActivityUser",
            "post_act",
            "disc_post_act",
            "Discussion for Post",
            "Activity Community",
            "Activity Hub",
            "Activity Space"), Times.Once);
    }

    [Test]
    public async Task PostCreatedActivityHandler_PostNotFound_DoesNotBroadcast()
    {
        var handler = new PostCreatedActivityHandler(_mockBroadcaster.Object, _context);
        var @event = new PostCreatedEvent(
            PostId.From("nonexistent_post"),
            DiscussionId.From("nonexistent_disc"),
            UserId.From("nonexistent_user"));

        await handler.HandleAsync(@event);

        _mockBroadcaster.Verify(b => b.BroadcastPostCreated(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    #endregion

    #region DiscussionCreatedActivityHandler Tests

    [Test]
    public async Task DiscussionCreatedActivityHandler_BroadcastsDiscussionCreated()
    {
        var (user, community, hub, space) = await SetupHierarchy();
        var discussion = new DiscussionDatabaseEntity
        {
            PublicId = "disc_created_act",
            Title = "New Discussion",
            Slug = "new-discussion",
            SpaceId = space.Id,
            CreatedByUserId = user.Id,
            CreatedAt = DateTime.UtcNow
        };
        _context.Discussions.Add(discussion);
        await _context.SaveChangesAsync();

        var handler = new DiscussionCreatedActivityHandler(_mockBroadcaster.Object, _context);
        var @event = new DiscussionCreatedEvent(
            DiscussionId.From("disc_created_act"),
            SpaceId.From("space_act"),
            UserId.From("user_activity"));

        await handler.HandleAsync(@event);

        _mockBroadcaster.Verify(b => b.BroadcastDiscussionCreated(
            "user_activity",
            "ActivityUser",
            "disc_created_act",
            "New Discussion",
            "Activity Community",
            "Activity Hub",
            "Activity Space"), Times.Once);
    }

    [Test]
    public async Task DiscussionCreatedActivityHandler_DiscussionNotFound_DoesNotBroadcast()
    {
        var handler = new DiscussionCreatedActivityHandler(_mockBroadcaster.Object, _context);
        var @event = new DiscussionCreatedEvent(
            DiscussionId.From("nonexistent"),
            SpaceId.From("nonexistent"),
            UserId.From("nonexistent"));

        await handler.HandleAsync(@event);

        _mockBroadcaster.Verify(b => b.BroadcastDiscussionCreated(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>()), Times.Never);
    }

    #endregion

    #region UserCreatedActivityHandler Tests

    [Test]
    public async Task UserCreatedActivityHandler_BroadcastsUserRegistered()
    {
        var user = new UserDatabaseEntity
        {
            PublicId = "new_user_act",
            DisplayName = "NewUser",
            Email = "newuser@example.com",
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var handler = new UserCreatedActivityHandler(_mockBroadcaster.Object, _context);
        var @event = new UserCreatedEvent(UserId.From("new_user_act"));

        await handler.HandleAsync(@event);

        _mockBroadcaster.Verify(b => b.BroadcastUserRegistered(
            "new_user_act",
            "NewUser",
            "newuser@example.com"), Times.Once);
    }

    [Test]
    public async Task UserCreatedActivityHandler_UserNotFound_DoesNotBroadcast()
    {
        var handler = new UserCreatedActivityHandler(_mockBroadcaster.Object, _context);
        var @event = new UserCreatedEvent(UserId.From("nonexistent"));

        await handler.HandleAsync(@event);

        _mockBroadcaster.Verify(b => b.BroadcastUserRegistered(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task UserCreatedActivityHandler_NullEmail_BroadcastsEmptyString()
    {
        var user = new UserDatabaseEntity
        {
            PublicId = "no_email_user",
            DisplayName = "NoEmail",
            Email = null,
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var handler = new UserCreatedActivityHandler(_mockBroadcaster.Object, _context);
        var @event = new UserCreatedEvent(UserId.From("no_email_user"));

        await handler.HandleAsync(@event);

        _mockBroadcaster.Verify(b => b.BroadcastUserRegistered(
            "no_email_user",
            "NoEmail",
            ""), Times.Once);
    }

    #endregion

    #region ReactionAddedActivityHandler Tests

    [Test]
    public async Task ReactionAddedActivityHandler_BroadcastsReactionAdded()
    {
        var (user, community, hub, space) = await SetupHierarchy();
        var discussion = new DiscussionDatabaseEntity
        {
            PublicId = "disc_react_act",
            Title = "Reaction Discussion",
            Slug = "react-disc",
            SpaceId = space.Id,
            CreatedByUserId = user.Id,
            CreatedAt = DateTime.UtcNow
        };
        _context.Discussions.Add(discussion);
        await _context.SaveChangesAsync();

        var post = new PostDatabaseEntity
        {
            PublicId = "post_react_act",
            Content = "Post to react to",
            DiscussionId = discussion.Id,
            CreatedByUserId = user.Id,
            CreatedAt = DateTime.UtcNow,
            LastModifiedAt = DateTime.UtcNow
        };
        _context.Posts.Add(post);
        await _context.SaveChangesAsync();

        var reaction = new ReactionDatabaseEntity
        {
            PublicId = "react_act",
            TypeId = (int)ReactionTypeEnum.ThumbsUp,
            PostId = post.Id,
            UserId = user.Id,
            CreatedAt = DateTime.UtcNow
        };
        _context.Reactions.Add(reaction);
        await _context.SaveChangesAsync();

        var handler = new ReactionAddedActivityHandler(_mockBroadcaster.Object, _context);
        var @event = new ReactionAddedEvent(
            ReactionId.From("react_act"),
            PostId.From("post_react_act"),
            UserId.From("user_activity"),
            ReactionType.ThumbsUp);

        await handler.HandleAsync(@event);

        _mockBroadcaster.Verify(b => b.BroadcastReactionAdded(
            "user_activity",
            "ActivityUser",
            "ThumbsUp",
            "post",
            "post_react_act",
            "Reaction Discussion"), Times.Once);
    }

    [Test]
    public async Task ReactionAddedActivityHandler_ReactionNotFound_DoesNotBroadcast()
    {
        var handler = new ReactionAddedActivityHandler(_mockBroadcaster.Object, _context);
        var @event = new ReactionAddedEvent(
            ReactionId.From("nonexistent"),
            PostId.From("nonexistent"),
            UserId.From("nonexistent"),
            ReactionType.ThumbsUp);

        await handler.HandleAsync(@event);

        _mockBroadcaster.Verify(b => b.BroadcastReactionAdded(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    #endregion

    #region FollowCreatedActivityHandler Tests

    [Test]
    public async Task FollowCreatedActivityHandler_UserFollow_BroadcastsCorrectly()
    {
        var follower = new UserDatabaseEntity
        {
            PublicId = "follower",
            DisplayName = "Follower",
            Email = "follower@e.com",
            CreatedAt = DateTime.UtcNow
        };
        var followed = new UserDatabaseEntity
        {
            PublicId = "followed",
            DisplayName = "Followed User",
            Email = "followed@e.com",
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.AddRange(follower, followed);
        await _context.SaveChangesAsync();

        var follow = new FollowDatabaseEntity
        {
            PublicId = "follow_act",
            TargetTypeId = (int)FollowTargetTypeEnum.User,
            LevelId = (int)FollowLevelEnum.DiscussionsAndPosts,
            UserId = follower.Id,
            FollowedUserId = followed.Id,
            CreatedAt = DateTime.UtcNow
        };
        _context.Follows.Add(follow);
        await _context.SaveChangesAsync();

        var handler = new FollowCreatedActivityHandler(_mockBroadcaster.Object, _context);
        var @event = new FollowCreatedEvent(
            FollowId.From("follow_act"),
            UserId.From("follower"),
            FollowTargetType.User,
            "followed");

        await handler.HandleAsync(@event);

        _mockBroadcaster.Verify(b => b.BroadcastFollowAdded(
            "follower",
            "Follower",
            "user",
            "followed",
            "Followed User"), Times.Once);
    }

    [Test]
    public async Task FollowCreatedActivityHandler_FollowNotFound_DoesNotBroadcast()
    {
        var handler = new FollowCreatedActivityHandler(_mockBroadcaster.Object, _context);
        var @event = new FollowCreatedEvent(
            FollowId.From("nonexistent"),
            UserId.From("nonexistent"),
            FollowTargetType.User,
            "target");

        await handler.HandleAsync(@event);

        _mockBroadcaster.Verify(b => b.BroadcastFollowAdded(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
    }

    #endregion
}
