using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Snakk.Application.Services;
using Snakk.Domain.Events;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.EventHandlers.Discord;

namespace Snakk.Infrastructure.Tests.EventHandlers.Discord;

public class DiscordEventHandlerTests : IDisposable
{
    private readonly SnakkDbContext _context;
    private readonly IDiscordNotificationService _discordService;

    public DiscordEventHandlerTests()
    {
        var options = new DbContextOptionsBuilder<SnakkDbContext>()
            .UseInMemoryDatabase(databaseName: $"DiscordHandlerTests_{Guid.NewGuid()}")
            .Options;
        _context = new SnakkDbContext(options);
        _discordService = Substitute.For<IDiscordNotificationService>();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private async Task<(UserDatabaseEntity user, CommunityDatabaseEntity community, HubDatabaseEntity hub, SpaceDatabaseEntity space, DiscussionDatabaseEntity discussion)> SetupHierarchy()
    {
        var user = new UserDatabaseEntity
        {
            PublicId = "user_discord",
            DisplayName = "DiscordUser",
            Email = "discord@example.com",
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);

        var community = new CommunityDatabaseEntity
        {
            PublicId = "comm_discord",
            Name = "Discord Community",
            Slug = "discord-community",
            CreatedAt = DateTime.UtcNow,
            VisibilityId = 1
        };
        _context.Communities.Add(community);
        await _context.SaveChangesAsync();

        var hub = new HubDatabaseEntity
        {
            PublicId = "hub_discord",
            Name = "Discord Hub",
            Slug = "discord-hub",
            CommunityId = community.Id,
            CreatedAt = DateTime.UtcNow
        };
        _context.Hubs.Add(hub);
        await _context.SaveChangesAsync();

        var space = new SpaceDatabaseEntity
        {
            PublicId = "space_discord",
            Name = "Discord Space",
            Slug = "discord-space",
            HubId = hub.Id,
            CreatedAt = DateTime.UtcNow
        };
        _context.Spaces.Add(space);
        await _context.SaveChangesAsync();

        var discussion = new DiscussionDatabaseEntity
        {
            PublicId = "disc_discord",
            Title = "Discord Discussion",
            Slug = "discord-discussion",
            SpaceId = space.Id,
            CreatedByUserId = user.Id,
            CreatedAt = DateTime.UtcNow
        };
        _context.Discussions.Add(discussion);
        await _context.SaveChangesAsync();

        return (user, community, hub, space, discussion);
    }

    #region DiscussionCreatedDiscordHandler Tests

    [Test]
    public async Task DiscussionCreatedDiscordHandler_DiscussionFound_NotifiesWithCorrectUrl()
    {
        // Arrange
        await SetupHierarchy();

        var handler = new DiscussionCreatedDiscordHandler(_discordService, _context);
        var @event = new DiscussionCreatedEvent(
            DiscussionId.From("disc_discord"),
            SpaceId.From("space_discord"),
            UserId.From("user_discord"));

        // Act
        await handler.HandleAsync(@event);

        // Assert
        await _discordService.Received(1).NotifyDiscussionCreatedAsync(
            "space_discord",
            "Discord Discussion",
            "/c/discord-community/h/discord-hub/discord-space/discord-discussion",
            "DiscordUser",
            "Discord Space",
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DiscussionCreatedDiscordHandler_DiscussionNotFound_DoesNotNotify()
    {
        // Arrange
        var handler = new DiscussionCreatedDiscordHandler(_discordService, _context);
        var @event = new DiscussionCreatedEvent(
            DiscussionId.From("nonexistent_disc"),
            SpaceId.From("nonexistent_space"),
            UserId.From("nonexistent_user"));

        // Act
        await handler.HandleAsync(@event);

        // Assert
        await _discordService.DidNotReceive().NotifyDiscussionCreatedAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    #endregion

    #region PostCreatedDiscordHandler Tests

    [Test]
    public async Task PostCreatedDiscordHandler_SecondPost_NotifiesPostCreated()
    {
        // Arrange
        var (user, _, _, _, discussion) = await SetupHierarchy();

        // First post (opening post, will be skipped by handler)
        var firstPost = new PostDatabaseEntity
        {
            PublicId = "post_discord_1",
            Content = "Opening post",
            DiscussionId = discussion.Id,
            CreatedByUserId = user.Id,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            LastModifiedAt = DateTime.UtcNow.AddMinutes(-5)
        };
        _context.Posts.Add(firstPost);
        await _context.SaveChangesAsync();

        // Second post — PostIndex will be 2
        var secondPost = new PostDatabaseEntity
        {
            PublicId = "post_discord_2",
            Content = "Reply post",
            DiscussionId = discussion.Id,
            CreatedByUserId = user.Id,
            CreatedAt = DateTime.UtcNow,
            LastModifiedAt = DateTime.UtcNow
        };
        _context.Posts.Add(secondPost);
        await _context.SaveChangesAsync();

        var handler = new PostCreatedDiscordHandler(_discordService, _context);
        var @event = new PostCreatedEvent(
            PostId.From("post_discord_2"),
            DiscussionId.From("disc_discord"),
            UserId.From("user_discord"));

        // Act
        await handler.HandleAsync(@event);

        // Assert
        await _discordService.Received(1).NotifyPostCreatedAsync(
            "space_discord",
            "Discord Discussion",
            "/c/discord-community/h/discord-hub/discord-space/discord-discussion",
            "DiscordUser",
            "Discord Space",
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task PostCreatedDiscordHandler_FirstPost_SkipsNotification()
    {
        // Arrange
        var (user, _, _, _, discussion) = await SetupHierarchy();

        var firstPost = new PostDatabaseEntity
        {
            PublicId = "post_discord_only",
            Content = "Only post in discussion",
            DiscussionId = discussion.Id,
            CreatedByUserId = user.Id,
            CreatedAt = DateTime.UtcNow,
            LastModifiedAt = DateTime.UtcNow
        };
        _context.Posts.Add(firstPost);
        await _context.SaveChangesAsync();

        var handler = new PostCreatedDiscordHandler(_discordService, _context);
        var @event = new PostCreatedEvent(
            PostId.From("post_discord_only"),
            DiscussionId.From("disc_discord"),
            UserId.From("user_discord"));

        // Act
        await handler.HandleAsync(@event);

        // Assert — opening post should be skipped
        await _discordService.DidNotReceive().NotifyPostCreatedAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task PostCreatedDiscordHandler_PostNotFound_DoesNotNotify()
    {
        // Arrange
        var handler = new PostCreatedDiscordHandler(_discordService, _context);
        var @event = new PostCreatedEvent(
            PostId.From("nonexistent_post"),
            DiscussionId.From("nonexistent_disc"),
            UserId.From("nonexistent_user"));

        // Act
        await handler.HandleAsync(@event);

        // Assert
        await _discordService.DidNotReceive().NotifyPostCreatedAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    #endregion
}
