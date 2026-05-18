using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Snakk.Application.Services;
using Snakk.Domain.Events;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.EventHandlers.Activity;

namespace Snakk.Infrastructure.Tests.EventHandlers.Activity;

public class RemainingActivityEventHandlerTests : IDisposable
{
    private readonly SnakkDbContext _context;
    private readonly IActivityBroadcaster _broadcaster;

    public RemainingActivityEventHandlerTests()
    {
        var options = new DbContextOptionsBuilder<SnakkDbContext>()
            .UseInMemoryDatabase(databaseName: $"RemainingActivityHandlerTests_{Guid.NewGuid()}")
            .Options;
        _context = new SnakkDbContext(options);
        _broadcaster = Substitute.For<IActivityBroadcaster>();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private async Task<(UserDatabaseEntity user, DiscussionDatabaseEntity discussion, PostDatabaseEntity post)> SetupUserDiscussionAndPost()
    {
        var user = new UserDatabaseEntity
        {
            PublicId = "user_rem",
            DisplayName = "RemUser",
            Email = "rem@example.com",
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);

        var community = new CommunityDatabaseEntity
        {
            PublicId = "comm_rem",
            Name = "Rem Community",
            Slug = "rem-community",
            CreatedAt = DateTime.UtcNow,
            VisibilityId = 1
        };
        _context.Communities.Add(community);
        await _context.SaveChangesAsync();

        var hub = new HubDatabaseEntity
        {
            PublicId = "hub_rem",
            Name = "Rem Hub",
            Slug = "rem-hub",
            CommunityId = community.Id,
            CreatedAt = DateTime.UtcNow
        };
        _context.Hubs.Add(hub);
        await _context.SaveChangesAsync();

        var space = new SpaceDatabaseEntity
        {
            PublicId = "space_rem",
            Name = "Rem Space",
            Slug = "rem-space",
            HubId = hub.Id,
            CreatedAt = DateTime.UtcNow
        };
        _context.Spaces.Add(space);
        await _context.SaveChangesAsync();

        var discussion = new DiscussionDatabaseEntity
        {
            PublicId = "disc_rem",
            Title = "Rem Discussion",
            Slug = "rem-discussion",
            SpaceId = space.Id,
            CreatedByUserId = user.Id,
            CreatedAt = DateTime.UtcNow
        };
        _context.Discussions.Add(discussion);
        await _context.SaveChangesAsync();

        var post = new PostDatabaseEntity
        {
            PublicId = "post_rem",
            Content = "Rem post content",
            DiscussionId = discussion.Id,
            CreatedByUserId = user.Id,
            CreatedAt = DateTime.UtcNow,
            LastModifiedAt = DateTime.UtcNow
        };
        _context.Posts.Add(post);
        await _context.SaveChangesAsync();

        return (user, discussion, post);
    }

    #region PostDeletedActivityHandler Tests

    [Test]
    public async Task PostDeletedActivityHandler_HardDelete_BroadcastsWithHardDeleteLabel()
    {
        // Arrange
        var (user, _, _) = await SetupUserDiscussionAndPost();

        var handler = new PostDeletedActivityHandler(_broadcaster, _context);
        var @event = new PostDeletedEvent(
            PostId.From("post_rem"),
            DiscussionId.From("disc_rem"),
            UserId.From("user_rem"),
            IsHardDelete: true);

        // Act
        await handler.HandleAsync(@event);

        // Assert
        _broadcaster.Received(1).BroadcastContentDeleted(
            "user_rem",
            "RemUser",
            "Post (hard delete)",
            "post_rem",
            null);
    }

    [Test]
    public async Task PostDeletedActivityHandler_SoftDelete_BroadcastsWithPostLabel()
    {
        // Arrange
        var (user, _, _) = await SetupUserDiscussionAndPost();

        var handler = new PostDeletedActivityHandler(_broadcaster, _context);
        var @event = new PostDeletedEvent(
            PostId.From("post_rem"),
            DiscussionId.From("disc_rem"),
            UserId.From("user_rem"),
            IsHardDelete: false);

        // Act
        await handler.HandleAsync(@event);

        // Assert
        _broadcaster.Received(1).BroadcastContentDeleted(
            "user_rem",
            "RemUser",
            "Post",
            "post_rem",
            null);
    }

    [Test]
    public async Task PostDeletedActivityHandler_UserNotFound_DoesNotBroadcast()
    {
        // Arrange
        var handler = new PostDeletedActivityHandler(_broadcaster, _context);
        var @event = new PostDeletedEvent(
            PostId.From("post_rem"),
            DiscussionId.From("disc_rem"),
            UserId.From("nonexistent_user"),
            IsHardDelete: false);

        // Act
        await handler.HandleAsync(@event);

        // Assert
        _broadcaster.DidNotReceive().BroadcastContentDeleted(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string>());
    }

    #endregion

    #region PostEditedActivityHandler Tests

    [Test]
    public async Task PostEditedActivityHandler_PostFound_BroadcastsModerationAction()
    {
        // Arrange
        await SetupUserDiscussionAndPost();

        var handler = new PostEditedActivityHandler(_broadcaster, _context);
        var @event = new PostEditedEvent(
            PostId.From("post_rem"),
            DiscussionId.From("disc_rem"));

        // Act
        await handler.HandleAsync(@event);

        // Assert
        _broadcaster.Received(1).BroadcastModerationAction(
            "user_rem",
            "RemUser",
            "PostEdited",
            "Post",
            "post_rem",
            "Rem Discussion",
            null);
    }

    [Test]
    public async Task PostEditedActivityHandler_PostNotFound_DoesNotBroadcast()
    {
        // Arrange
        var handler = new PostEditedActivityHandler(_broadcaster, _context);
        var @event = new PostEditedEvent(
            PostId.From("nonexistent_post"),
            DiscussionId.From("nonexistent_disc"));

        // Act
        await handler.HandleAsync(@event);

        // Assert
        _broadcaster.DidNotReceive().BroadcastModerationAction(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    #endregion

    #region ReactionRemovedActivityHandler Tests

    [Test]
    public async Task ReactionRemovedActivityHandler_UserAndPostFound_BroadcastsReactionRemoved()
    {
        // Arrange
        await SetupUserDiscussionAndPost();

        var handler = new ReactionRemovedActivityHandler(_broadcaster, _context);
        var @event = new ReactionRemovedEvent(
            ReactionId.From("react_rem"),
            PostId.From("post_rem"),
            UserId.From("user_rem"),
            ReactionType.Agree);

        // Act
        await handler.HandleAsync(@event);

        // Assert
        _broadcaster.Received(1).BroadcastReactionAdded(
            "user_rem",
            "RemUser",
            "Agree (removed)",
            "post",
            "post_rem",
            "Rem Discussion");
    }

    [Test]
    public async Task ReactionRemovedActivityHandler_UserNotFound_DoesNotBroadcast()
    {
        // Arrange
        await SetupUserDiscussionAndPost();

        var handler = new ReactionRemovedActivityHandler(_broadcaster, _context);
        var @event = new ReactionRemovedEvent(
            ReactionId.From("react_rem"),
            PostId.From("post_rem"),
            UserId.From("nonexistent_user"),
            ReactionType.Love);

        // Act
        await handler.HandleAsync(@event);

        // Assert
        _broadcaster.DidNotReceive().BroadcastReactionAdded(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task ReactionRemovedActivityHandler_PostNotFound_DoesNotBroadcast()
    {
        // Arrange
        var (user, _, _) = await SetupUserDiscussionAndPost();

        var handler = new ReactionRemovedActivityHandler(_broadcaster, _context);
        var @event = new ReactionRemovedEvent(
            ReactionId.From("react_rem"),
            PostId.From("nonexistent_post"),
            UserId.From("user_rem"),
            ReactionType.Fire);

        // Act
        await handler.HandleAsync(@event);

        // Assert
        _broadcaster.DidNotReceive().BroadcastReactionAdded(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    #endregion
}
