using Microsoft.EntityFrameworkCore;
using Snakk.Domain.Events;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.EventHandlers.Trending;
using Snakk.Shared.Enums;

namespace Snakk.Infrastructure.Tests.EventHandlers.Trending;

public class TrendingEventHandlerTests : IDisposable
{
    private readonly SnakkDbContext _context;

    public TrendingEventHandlerTests()
    {
        var options = new DbContextOptionsBuilder<SnakkDbContext>()
            .UseInMemoryDatabase(databaseName: $"TrendingHandlerTests_{Guid.NewGuid()}")
            .Options;
        _context = new SnakkDbContext(options);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private async Task<(UserDatabaseEntity user, DiscussionDatabaseEntity discussion)> SetupDiscussion(
        string discPid = "disc_trend", bool isDeleted = false)
    {
        var user = new UserDatabaseEntity
        {
            PublicId = "user_trend",
            DisplayName = "TrendUser",
            Email = "trend@example.com",
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);

        var community = new CommunityDatabaseEntity
        {
            PublicId = "comm_trend",
            Name = "Trend Community",
            Slug = "trend-community",
            CreatedAt = DateTime.UtcNow,
            VisibilityId = 1
        };
        _context.Communities.Add(community);
        await _context.SaveChangesAsync();

        var hub = new HubDatabaseEntity
        {
            PublicId = "hub_trend",
            Name = "Trend Hub",
            Slug = "trend-hub",
            CommunityId = community.Id,
            CreatedAt = DateTime.UtcNow
        };
        _context.Hubs.Add(hub);
        await _context.SaveChangesAsync();

        var space = new SpaceDatabaseEntity
        {
            PublicId = "space_trend",
            Name = "Trend Space",
            Slug = "trend-space",
            HubId = hub.Id,
            CreatedAt = DateTime.UtcNow
        };
        _context.Spaces.Add(space);
        await _context.SaveChangesAsync();

        var discussion = new DiscussionDatabaseEntity
        {
            PublicId = discPid,
            Title = "Trend Discussion",
            Slug = "trend-discussion",
            SpaceId = space.Id,
            CreatedByUserId = user.Id,
            IsDeleted = isDeleted,
            TrendScore = 0,
            CreatedAt = DateTime.UtcNow
        };
        _context.Discussions.Add(discussion);
        await _context.SaveChangesAsync();

        return (user, discussion);
    }

    #region PostCreatedTrendingHandler Tests

    [Test]
    public async Task PostCreatedTrendingHandler_WithRecentPost_IncreasesTrendScore()
    {
        // Arrange
        var (user, discussion) = await SetupDiscussion();

        _context.Posts.Add(new PostDatabaseEntity
        {
            PublicId = "post_trend_1",
            Content = "Trending post",
            DiscussionId = discussion.Id,
            CreatedByUserId = user.Id,
            CreatedAt = DateTime.UtcNow,
            LastModifiedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var handler = new PostCreatedTrendingHandler(_context);
        var @event = new PostCreatedEvent(
            PostId.From("post_trend_1"),
            DiscussionId.From("disc_trend"),
            UserId.From("user_trend"));

        // Act
        await handler.HandleAsync(@event);

        // Assert
        var updated = await _context.Discussions.SingleAsync(d => d.PublicId == "disc_trend");
        await Assert.That(updated.TrendScore).IsGreaterThan(0);
    }

    [Test]
    public async Task PostCreatedTrendingHandler_DiscussionNotFound_DoesNotThrow()
    {
        // Arrange
        var handler = new PostCreatedTrendingHandler(_context);
        var @event = new PostCreatedEvent(
            PostId.From("post_trend_x"),
            DiscussionId.From("nonexistent_disc"),
            UserId.From("user_trend"));

        // Act & Assert
        await Assert.That(async () => await handler.HandleAsync(@event)).ThrowsNothing();
    }

    [Test]
    public async Task PostCreatedTrendingHandler_DeletedDiscussion_DoesNotUpdateScore()
    {
        // Arrange
        var (user, discussion) = await SetupDiscussion("disc_trend_deleted", isDeleted: true);
        var originalScore = discussion.TrendScore;

        _context.Posts.Add(new PostDatabaseEntity
        {
            PublicId = "post_trend_del",
            Content = "Post on deleted discussion",
            DiscussionId = discussion.Id,
            CreatedByUserId = user.Id,
            CreatedAt = DateTime.UtcNow,
            LastModifiedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var handler = new PostCreatedTrendingHandler(_context);
        var @event = new PostCreatedEvent(
            PostId.From("post_trend_del"),
            DiscussionId.From("disc_trend_deleted"),
            UserId.From("user_trend"));

        // Act
        await handler.HandleAsync(@event);

        // Assert — use IgnoreQueryFilters because deleted discussions are filtered by default
        var reloaded = await _context.Discussions.IgnoreQueryFilters().SingleAsync(d => d.PublicId == "disc_trend_deleted");
        await Assert.That(reloaded.TrendScore).IsEqualTo(originalScore);
    }

    #endregion

    #region ReactionAddedTrendingHandler Tests

    [Test]
    public async Task ReactionAddedTrendingHandler_WithRecentReaction_IncreasesTrendScore()
    {
        // Arrange
        var (user, discussion) = await SetupDiscussion("disc_trend_ra");

        var post = new PostDatabaseEntity
        {
            PublicId = "post_trend_ra",
            Content = "Post for reaction trending",
            DiscussionId = discussion.Id,
            CreatedByUserId = user.Id,
            CreatedAt = DateTime.UtcNow,
            LastModifiedAt = DateTime.UtcNow
        };
        _context.Posts.Add(post);
        await _context.SaveChangesAsync();

        _context.PostReactions.Add(new PostReactionDatabaseEntity
        {
            PublicId = "react_trend",
            TypeId = (int)ReactionTypeEnum.Agree,
            PostId = post.Id,
            UserId = user.Id,
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var handler = new ReactionAddedTrendingHandler(_context);
        var @event = new ReactionAddedEvent(
            ReactionId.From("react_trend"),
            PostId.From("post_trend_ra"),
            UserId.From("user_trend"),
            ReactionType.Agree);

        // Act
        await handler.HandleAsync(@event);

        // Assert
        var updated = await _context.Discussions.SingleAsync(d => d.PublicId == "disc_trend_ra");
        await Assert.That(updated.TrendScore).IsGreaterThan(0);
    }

    [Test]
    public async Task ReactionAddedTrendingHandler_PostNotFound_DoesNotThrow()
    {
        // Arrange
        var handler = new ReactionAddedTrendingHandler(_context);
        var @event = new ReactionAddedEvent(
            ReactionId.From("react_x"),
            PostId.From("nonexistent_post"),
            UserId.From("user_trend"),
            ReactionType.Love);

        // Act & Assert
        await Assert.That(async () => await handler.HandleAsync(@event)).ThrowsNothing();
    }

    #endregion

    #region ReactionRemovedTrendingHandler Tests

    [Test]
    public async Task ReactionRemovedTrendingHandler_PostFound_RecalculatesTrendScore()
    {
        // Arrange
        var (user, discussion) = await SetupDiscussion("disc_trend_rr");

        var post = new PostDatabaseEntity
        {
            PublicId = "post_trend_rr",
            Content = "Post for removed reaction trending",
            DiscussionId = discussion.Id,
            CreatedByUserId = user.Id,
            CreatedAt = DateTime.UtcNow,
            LastModifiedAt = DateTime.UtcNow
        };
        _context.Posts.Add(post);
        await _context.SaveChangesAsync();

        var handler = new ReactionRemovedTrendingHandler(_context);
        var @event = new ReactionRemovedEvent(
            ReactionId.From("react_rr"),
            PostId.From("post_trend_rr"),
            UserId.From("user_trend"),
            ReactionType.Thanks);

        // Act & Assert — should not throw, even with reaction already removed
        await Assert.That(async () => await handler.HandleAsync(@event)).ThrowsNothing();

        // Score is recalculated (post within window, no reaction = post-only contribution)
        var updated = await _context.Discussions.SingleAsync(d => d.PublicId == "disc_trend_rr");
        await Assert.That(updated.TrendScore).IsGreaterThan(0);
    }

    [Test]
    public async Task ReactionRemovedTrendingHandler_PostNotFound_DoesNotThrow()
    {
        // Arrange
        var handler = new ReactionRemovedTrendingHandler(_context);
        var @event = new ReactionRemovedEvent(
            ReactionId.From("react_rr_x"),
            PostId.From("nonexistent_post"),
            UserId.From("user_trend"),
            ReactionType.Fire);

        // Act & Assert
        await Assert.That(async () => await handler.HandleAsync(@event)).ThrowsNothing();
    }

    #endregion
}
