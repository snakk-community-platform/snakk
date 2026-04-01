using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Snakk.Application.Services;
using Snakk.Domain.Events;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.EventHandlers.Activity;
using Snakk.Shared.Enums;

namespace Snakk.Infrastructure.Tests.EventHandlers.Activity;

public class ReactionActivityHandlerTests : IDisposable
{
    private readonly SnakkDbContext _context;
    private readonly IActivityBroadcaster _broadcaster;

    public ReactionActivityHandlerTests()
    {
        var options = new DbContextOptionsBuilder<SnakkDbContext>()
            .UseInMemoryDatabase(databaseName: $"ReactionActivityHandlerTests_{Guid.NewGuid()}")
            .Options;
        _context = new SnakkDbContext(options);
        _broadcaster = Substitute.For<IActivityBroadcaster>();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private async Task<(UserDatabaseEntity user, CommunityDatabaseEntity community, HubDatabaseEntity hub, SpaceDatabaseEntity space, DiscussionDatabaseEntity discussion, PostDatabaseEntity post)> SetupFullHierarchyWithPost()
    {
        var user = new UserDatabaseEntity
        {
            PublicId = "user_react",
            DisplayName = "ReactUser",
            Email = "react@example.com",
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);

        var community = new CommunityDatabaseEntity
        {
            PublicId = "comm_react",
            Name = "Reaction Community",
            Slug = "reaction-community",
            CreatedAt = DateTime.UtcNow,
            VisibilityId = 1
        };
        _context.Communities.Add(community);
        await _context.SaveChangesAsync();

        var hub = new HubDatabaseEntity
        {
            PublicId = "hub_react",
            Name = "Reaction Hub",
            Slug = "reaction-hub",
            CommunityId = community.Id,
            CreatedAt = DateTime.UtcNow
        };
        _context.Hubs.Add(hub);
        await _context.SaveChangesAsync();

        var space = new SpaceDatabaseEntity
        {
            PublicId = "space_react",
            Name = "Reaction Space",
            Slug = "reaction-space",
            HubId = hub.Id,
            CreatedAt = DateTime.UtcNow
        };
        _context.Spaces.Add(space);
        await _context.SaveChangesAsync();

        var discussion = new DiscussionDatabaseEntity
        {
            PublicId = "disc_react",
            Title = "Reaction Discussion",
            Slug = "reaction-discussion",
            SpaceId = space.Id,
            CreatedByUserId = user.Id,
            CreatedAt = DateTime.UtcNow
        };
        _context.Discussions.Add(discussion);
        await _context.SaveChangesAsync();

        var post = new PostDatabaseEntity
        {
            PublicId = "post_react",
            Content = "Post for reactions",
            DiscussionId = discussion.Id,
            CreatedByUserId = user.Id,
            CreatedAt = DateTime.UtcNow
        };
        _context.Posts.Add(post);
        await _context.SaveChangesAsync();

        return (user, community, hub, space, discussion, post);
    }

    [Test]
    public async Task HandleAsync_ReactionNotFound_DoesNotBroadcast()
    {
        var handler = new ReactionAddedActivityHandler(_broadcaster, _context);
        var @event = new ReactionAddedEvent(
            ReactionId.From("nonexistent"),
            PostId.From("nonexistent"),
            UserId.From("nonexistent"),
            ReactionType.Agree);

        await handler.HandleAsync(@event);

        _broadcaster.DidNotReceive().BroadcastReactionAdded(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task HandleAsync_AgreeReaction_BroadcastsCorrectReactionType()
    {
        var (user, _, _, _, discussion, post) = await SetupFullHierarchyWithPost();

        var reaction = new ReactionDatabaseEntity
        {
            PublicId = "react_agree",
            TypeId = (int)ReactionTypeEnum.Agree,
            PostId = post.Id,
            UserId = user.Id,
            CreatedAt = DateTime.UtcNow
        };
        _context.Reactions.Add(reaction);
        await _context.SaveChangesAsync();

        var handler = new ReactionAddedActivityHandler(_broadcaster, _context);
        var @event = new ReactionAddedEvent(
            ReactionId.From("react_agree"),
            PostId.From("post_react"),
            UserId.From("user_react"),
            ReactionType.Agree);

        await handler.HandleAsync(@event);

        _broadcaster.Received(1).BroadcastReactionAdded(
            "user_react",
            "ReactUser",
            "Agree",
            "post",
            "post_react",
            "Reaction Discussion");
    }

    [Test]
    public async Task HandleAsync_LoveReaction_BroadcastsCorrectReactionType()
    {
        var (user, _, _, _, discussion, post) = await SetupFullHierarchyWithPost();

        var reaction = new ReactionDatabaseEntity
        {
            PublicId = "react_love",
            TypeId = (int)ReactionTypeEnum.Love,
            PostId = post.Id,
            UserId = user.Id,
            CreatedAt = DateTime.UtcNow
        };
        _context.Reactions.Add(reaction);
        await _context.SaveChangesAsync();

        var handler = new ReactionAddedActivityHandler(_broadcaster, _context);
        var @event = new ReactionAddedEvent(
            ReactionId.From("react_love"),
            PostId.From("post_react"),
            UserId.From("user_react"),
            ReactionType.Love);

        await handler.HandleAsync(@event);

        _broadcaster.Received(1).BroadcastReactionAdded(
            "user_react",
            "ReactUser",
            "Love",
            "post",
            "post_react",
            "Reaction Discussion");
    }

    [Test]
    public async Task HandleAsync_AlwaysUsesPostAsTargetType()
    {
        var (user, _, _, _, _, post) = await SetupFullHierarchyWithPost();

        var reaction = new ReactionDatabaseEntity
        {
            PublicId = "react_target",
            TypeId = (int)ReactionTypeEnum.Watching,
            PostId = post.Id,
            UserId = user.Id,
            CreatedAt = DateTime.UtcNow
        };
        _context.Reactions.Add(reaction);
        await _context.SaveChangesAsync();

        var handler = new ReactionAddedActivityHandler(_broadcaster, _context);
        var @event = new ReactionAddedEvent(
            ReactionId.From("react_target"),
            PostId.From("post_react"),
            UserId.From("user_react"),
            ReactionType.Agree);

        await handler.HandleAsync(@event);

        _broadcaster.Received(1).BroadcastReactionAdded(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            "post",
            Arg.Any<string>(),
            Arg.Any<string>());
    }

    [Test]
    public async Task HandleAsync_BroadcastsUserIdFromEvent()
    {
        var (user, _, _, _, _, post) = await SetupFullHierarchyWithPost();

        var reaction = new ReactionDatabaseEntity
        {
            PublicId = "react_userid",
            TypeId = (int)ReactionTypeEnum.Agree,
            PostId = post.Id,
            UserId = user.Id,
            CreatedAt = DateTime.UtcNow
        };
        _context.Reactions.Add(reaction);
        await _context.SaveChangesAsync();

        var handler = new ReactionAddedActivityHandler(_broadcaster, _context);
        var @event = new ReactionAddedEvent(
            ReactionId.From("react_userid"),
            PostId.From("post_react"),
            UserId.From("user_react"),
            ReactionType.Agree);

        await handler.HandleAsync(@event);

        // Verify the userId comes from the event, not from the database lookup
        _broadcaster.Received(1).BroadcastReactionAdded(
            "user_react",
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>());
    }

    [Test]
    public async Task HandleAsync_ResolvesDisplayNameFromDatabase()
    {
        var (user, _, _, _, _, post) = await SetupFullHierarchyWithPost();

        var reaction = new ReactionDatabaseEntity
        {
            PublicId = "react_display",
            TypeId = (int)ReactionTypeEnum.MindBlown,
            PostId = post.Id,
            UserId = user.Id,
            CreatedAt = DateTime.UtcNow
        };
        _context.Reactions.Add(reaction);
        await _context.SaveChangesAsync();

        var handler = new ReactionAddedActivityHandler(_broadcaster, _context);
        var @event = new ReactionAddedEvent(
            ReactionId.From("react_display"),
            PostId.From("post_react"),
            UserId.From("user_react"),
            ReactionType.Agree);

        await handler.HandleAsync(@event);

        // Verify the display name "ReactUser" is resolved from the database entity
        _broadcaster.Received(1).BroadcastReactionAdded(
            Arg.Any<string>(),
            "ReactUser",
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>());
    }

    [Test]
    public async Task HandleAsync_ResolvesDiscussionTitleFromPostRelationship()
    {
        var (user, _, _, _, _, post) = await SetupFullHierarchyWithPost();

        var reaction = new ReactionDatabaseEntity
        {
            PublicId = "react_title",
            TypeId = (int)ReactionTypeEnum.Agree,
            PostId = post.Id,
            UserId = user.Id,
            CreatedAt = DateTime.UtcNow
        };
        _context.Reactions.Add(reaction);
        await _context.SaveChangesAsync();

        var handler = new ReactionAddedActivityHandler(_broadcaster, _context);
        var @event = new ReactionAddedEvent(
            ReactionId.From("react_title"),
            PostId.From("post_react"),
            UserId.From("user_react"),
            ReactionType.Agree);

        await handler.HandleAsync(@event);

        // Verify discussion title is resolved through the post -> discussion relationship
        _broadcaster.Received(1).BroadcastReactionAdded(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            "Reaction Discussion");
    }
}
