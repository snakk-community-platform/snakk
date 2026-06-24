using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Snakk.Domain.Events;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.EventHandlers.Trending;
using Snakk.Shared.Enums;
using StackExchange.Redis;

namespace Snakk.Infrastructure.Tests.EventHandlers.Trending;

public class TrendingEventHandlerTests : IDisposable
{
    private readonly SnakkDbContext _context;
    private readonly IDatabase _redisDb = Substitute.For<IDatabase>();
    private readonly IConnectionMultiplexer _redis = Substitute.For<IConnectionMultiplexer>();

    public TrendingEventHandlerTests()
    {
        var options = new DbContextOptionsBuilder<SnakkDbContext>()
            .UseInMemoryDatabase(databaseName: $"TrendingHandlerTests_{Guid.NewGuid()}")
            .Options;
        _context = new SnakkDbContext(options);
        _redis.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(_redisDb);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private async Task<(UserDatabaseEntity user, DiscussionDatabaseEntity discussion)> SetupDiscussion(
        string discPid = "disc_trend")
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
            TrendScore = 0,
            CreatedAt = DateTime.UtcNow
        };
        _context.Discussions.Add(discussion);
        await _context.SaveChangesAsync();

        return (user, discussion);
    }

    #region PostCreatedTrendingHandler Tests

    [Test]
    public async Task PostCreatedTrendingHandler_AddsDiscussionIdToDirtySet()
    {
        var handler = new PostCreatedTrendingHandler(_redis);
        var @event = new PostCreatedEvent(
            PostId.From("post_trend_1"),
            DiscussionId.From("disc_trend"),
            UserId.From("user_trend"));

        await handler.HandleAsync(@event);

        await _redisDb.Received(1).SetAddAsync(
            PostCreatedTrendingHandler.TrendDirtyKey,
            Arg.Any<RedisValue>(),
            Arg.Any<CommandFlags>());
    }

    #endregion

    #region ReactionAddedTrendingHandler Tests

    [Test]
    public async Task ReactionAddedTrendingHandler_PostFound_AddsDiscussionIdToDirtySet()
    {
        var (user, discussion) = await SetupDiscussion("disc_trend_ra");

        _context.Posts.Add(new PostDatabaseEntity
        {
            PublicId = "post_trend_ra",
            Content = "Post for reaction trending",
            DiscussionId = discussion.Id,
            CreatedByUserId = user.Id,
            CreatedAt = DateTime.UtcNow,
            LastModifiedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var handler = new ReactionAddedTrendingHandler(_context, _redis);
        var @event = new ReactionAddedEvent(
            ReactionId.From("react_trend"),
            PostId.From("post_trend_ra"),
            UserId.From("user_trend"),
            ReactionType.Agree);

        await handler.HandleAsync(@event);

        await _redisDb.Received(1).SetAddAsync(
            PostCreatedTrendingHandler.TrendDirtyKey,
            Arg.Any<RedisValue>(),
            Arg.Any<CommandFlags>());
    }

    [Test]
    public async Task ReactionAddedTrendingHandler_PostNotFound_DoesNotCallRedis()
    {
        var handler = new ReactionAddedTrendingHandler(_context, _redis);
        var @event = new ReactionAddedEvent(
            ReactionId.From("react_x"),
            PostId.From("nonexistent_post"),
            UserId.From("user_trend"),
            ReactionType.Love);

        await handler.HandleAsync(@event);

        _redisDb.DidNotReceiveWithAnyArgs().SetAddAsync((RedisKey)default, (RedisValue)default, default);
    }

    #endregion

    #region ReactionRemovedTrendingHandler Tests

    [Test]
    public async Task ReactionRemovedTrendingHandler_PostFound_AddsDiscussionIdToDirtySet()
    {
        var (user, discussion) = await SetupDiscussion("disc_trend_rr");

        _context.Posts.Add(new PostDatabaseEntity
        {
            PublicId = "post_trend_rr",
            Content = "Post for removed reaction trending",
            DiscussionId = discussion.Id,
            CreatedByUserId = user.Id,
            CreatedAt = DateTime.UtcNow,
            LastModifiedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var handler = new ReactionRemovedTrendingHandler(_context, _redis);
        var @event = new ReactionRemovedEvent(
            ReactionId.From("react_rr"),
            PostId.From("post_trend_rr"),
            UserId.From("user_trend"),
            ReactionType.Thanks);

        await handler.HandleAsync(@event);

        await _redisDb.Received(1).SetAddAsync(
            PostCreatedTrendingHandler.TrendDirtyKey,
            Arg.Any<RedisValue>(),
            Arg.Any<CommandFlags>());
    }

    [Test]
    public async Task ReactionRemovedTrendingHandler_PostNotFound_DoesNotCallRedis()
    {
        var handler = new ReactionRemovedTrendingHandler(_context, _redis);
        var @event = new ReactionRemovedEvent(
            ReactionId.From("react_rr_x"),
            PostId.From("nonexistent_post"),
            UserId.From("user_trend"),
            ReactionType.Fire);

        await handler.HandleAsync(@event);

        _redisDb.DidNotReceiveWithAnyArgs().SetAddAsync((RedisKey)default, (RedisValue)default, default);
    }

    #endregion
}
