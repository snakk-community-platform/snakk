using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.Services;
using Snakk.Application.DTOs.Management;
using Snakk.Shared.Enums;

namespace Snakk.Infrastructure.Tests.Services;

public class HubManagementServiceTests : IDisposable
{
    private readonly SnakkDbContext _context;
    private readonly HubManagementService _service;

    public HubManagementServiceTests()
    {
        var options = new DbContextOptionsBuilder<SnakkDbContext>()
            .UseInMemoryDatabase(databaseName: $"HubManagementServiceTests_{Guid.NewGuid()}")
            .Options;
        _context = new SnakkDbContext(options);
        _service = new HubManagementService(_context, Substitute.For<ILogger<HubManagementService>>());
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private async Task<(UserDatabaseEntity User, CommunityDatabaseEntity Community, HubDatabaseEntity Hub, SpaceDatabaseEntity Space)> CreateHierarchyAsync()
    {
        var user = new UserDatabaseEntity { PublicId = "user-001", DisplayName = "Test User", Email = "u@test.com", CreatedAt = DateTime.UtcNow };
        _context.Users.Add(user);

        var community = new CommunityDatabaseEntity { PublicId = "comm-001", Name = "Test Community", Slug = "test-community", CreatedAt = DateTime.UtcNow, VisibilityId = 1 };
        _context.Communities.Add(community);
        await _context.SaveChangesAsync();

        var hub = new HubDatabaseEntity { PublicId = "hub-001", CommunityId = community.Id, Name = "Test Hub", Slug = "test-hub", CreatedAt = DateTime.UtcNow };
        _context.Hubs.Add(hub);
        community.HubCount++;
        await _context.SaveChangesAsync();

        var space = new SpaceDatabaseEntity { PublicId = "space-001", HubId = hub.Id, Name = "Test Space", Slug = "test-space", CreatedAt = DateTime.UtcNow };
        _context.Spaces.Add(space);
        hub.SpaceCount++;
        community.SpaceCount++;
        await _context.SaveChangesAsync();

        return (user, community, hub, space);
    }

    private async Task<DiscussionDatabaseEntity> CreateDiscussionAsync(int spaceId, int userId, string title = "Test Discussion")
    {
        var discussion = new DiscussionDatabaseEntity
        {
            PublicId = $"disc-{Guid.NewGuid():N}",
            SpaceId = spaceId,
            CreatedByUserId = userId,
            Title = title,
            Slug = title.ToLower().Replace(" ", "-"),
            CreatedAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow
        };
        _context.Discussions.Add(discussion);

        var space = await _context.Spaces.FindAsync(spaceId);
        space!.DiscussionCount++;
        var hub = await _context.Hubs.FindAsync(space.HubId);
        hub!.DiscussionCount++;
        var community = await _context.Communities.FindAsync(hub.CommunityId);
        community!.DiscussionCount++;

        await _context.SaveChangesAsync();

        return discussion;
    }

    private async Task<PostDatabaseEntity> CreatePostAsync(int discussionId, int userId, string content = "Test post content", bool isFirstPost = false)
    {
        var post = new PostDatabaseEntity
        {
            PublicId = $"post-{Guid.NewGuid():N}",
            DiscussionId = discussionId,
            CreatedByUserId = userId,
            Content = content,
            CreatedAt = DateTime.UtcNow,
            IsFirstPost = isFirstPost
        };
        _context.Posts.Add(post);

        var discussion = await _context.Discussions.FindAsync(discussionId);
        discussion!.PostCount++;
        var space = await _context.Spaces.FindAsync(discussion.SpaceId);
        space!.PostCount++;
        var hub = await _context.Hubs.FindAsync(space.HubId);
        hub!.PostCount++;
        var community = await _context.Communities.FindAsync(hub.CommunityId);
        community!.PostCount++;

        await _context.SaveChangesAsync();

        return post;
    }

    #region GetOverviewAsync Tests

    [Test]
    public async Task GetOverviewAsync_ReturnsHubStatsWithSpaceDiscussionAndPostCounts()
    {
        var (user, community, hub, space) = await CreateHierarchyAsync();

        // Add a second space in the same hub
        var space2 = new SpaceDatabaseEntity { PublicId = "space-002", HubId = hub.Id, Name = "Space 2", Slug = "space-2", CreatedAt = DateTime.UtcNow };
        _context.Spaces.Add(space2);
        hub.SpaceCount++;
        community.SpaceCount++;
        await _context.SaveChangesAsync();

        // Create discussions and posts across both spaces
        var disc1 = await CreateDiscussionAsync(space.Id, user.Id, "Discussion 1");
        var disc2 = await CreateDiscussionAsync(space.Id, user.Id, "Discussion 2");
        var disc3 = await CreateDiscussionAsync(space2.Id, user.Id, "Discussion 3");

        await CreatePostAsync(disc1.Id, user.Id, "Post 1", isFirstPost: true);
        await CreatePostAsync(disc1.Id, user.Id, "Post 2");
        await CreatePostAsync(disc2.Id, user.Id, "Post 3", isFirstPost: true);
        await CreatePostAsync(disc3.Id, user.Id, "Post 4", isFirstPost: true);

        var result = await _service.GetOverviewAsync("hub-001");

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Name).IsEqualTo("Test Hub");
        await Assert.That(result.Slug).IsEqualTo("test-hub");
        await Assert.That(result.CommunitySlug).IsEqualTo("test-community");
        await Assert.That(result.CommunityName).IsEqualTo("Test Community");
        await Assert.That(result.TotalSpaces).IsEqualTo(2);
        await Assert.That(result.TotalDiscussions).IsEqualTo(3);
        await Assert.That(result.TotalPosts).IsEqualTo(4);
    }

    [Test]
    public async Task GetOverviewAsync_WhenHubNotFound_ReturnsNull()
    {
        // Create a community but no hub with matching slug
        var community = new CommunityDatabaseEntity { PublicId = "comm-empty", Name = "Empty Community", Slug = "empty-community", CreatedAt = DateTime.UtcNow, VisibilityId = 1 };
        _context.Communities.Add(community);
        await _context.SaveChangesAsync();

        var result = await _service.GetOverviewAsync("nonexistent-hub");

        await Assert.That(result).IsNull();
    }

    #endregion

    #region GetSettingsAsync Tests

    [Test]
    public async Task GetSettingsAsync_ReturnsHubNameAndDescription()
    {
        var community = new CommunityDatabaseEntity { PublicId = "comm-settings", Name = "Settings Community", Slug = "settings-community", CreatedAt = DateTime.UtcNow, VisibilityId = 1 };
        _context.Communities.Add(community);
        await _context.SaveChangesAsync();

        var hub = new HubDatabaseEntity
        {
            PublicId = "hub-settings",
            CommunityId = community.Id,
            Name = "Settings Hub",
            Slug = "settings-hub",
            Description = "A hub for settings testing",
            CreatedAt = DateTime.UtcNow
        };
        _context.Hubs.Add(hub);
        await _context.SaveChangesAsync();

        var result = await _service.GetSettingsAsync("hub-settings");

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Name).IsEqualTo("Settings Hub");
        await Assert.That(result.Description).IsEqualTo("A hub for settings testing");
        await Assert.That(result.Slug).IsEqualTo("settings-hub");
    }

    #endregion

    #region UpdateSettingsAsync Tests

    [Test]
    public async Task UpdateSettingsAsync_UpdatesNameAndDescription()
    {
        var community = new CommunityDatabaseEntity { PublicId = "comm-update", Name = "Update Community", Slug = "update-community", CreatedAt = DateTime.UtcNow, VisibilityId = 1 };
        _context.Communities.Add(community);
        await _context.SaveChangesAsync();

        var hub = new HubDatabaseEntity
        {
            PublicId = "hub-update",
            CommunityId = community.Id,
            Name = "Original Hub Name",
            Slug = "update-hub",
            Description = "Original description",
            CreatedAt = DateTime.UtcNow
        };
        _context.Hubs.Add(hub);
        await _context.SaveChangesAsync();

        var request = new UpdateHubSettingsRequest
        {
            Name = "Updated Hub Name",
            Description = "Updated hub description"
        };

        var result = await _service.UpdateSettingsAsync("hub-update", request);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Name).IsEqualTo("Updated Hub Name");
        await Assert.That(result.Description).IsEqualTo("Updated hub description");

        // Verify persistence
        var persisted = await _context.Hubs.FirstAsync(h => h.Slug == "update-hub");
        await Assert.That(persisted.Name).IsEqualTo("Updated Hub Name");
        await Assert.That(persisted.Description).IsEqualTo("Updated hub description");
    }

    #endregion

    #region GetSpacesAsync Tests

    [Test]
    public async Task GetSpacesAsync_ReturnsSpacesInHubWithDiscussionAndPostCounts()
    {
        var (user, community, hub, space) = await CreateHierarchyAsync();

        // Add a second space
        var space2 = new SpaceDatabaseEntity { PublicId = "space-002", HubId = hub.Id, Name = "Space Two", Slug = "space-two", CreatedAt = DateTime.UtcNow };
        _context.Spaces.Add(space2);
        hub.SpaceCount++;
        community.SpaceCount++;
        await _context.SaveChangesAsync();

        // Create discussions and posts in the first space
        var disc1 = await CreateDiscussionAsync(space.Id, user.Id, "Disc A");
        var disc2 = await CreateDiscussionAsync(space.Id, user.Id, "Disc B");
        await CreatePostAsync(disc1.Id, user.Id, "Post A", isFirstPost: true);
        await CreatePostAsync(disc1.Id, user.Id, "Post B");
        await CreatePostAsync(disc2.Id, user.Id, "Post C", isFirstPost: true);

        // Create one discussion with one post in the second space
        var disc3 = await CreateDiscussionAsync(space2.Id, user.Id, "Disc C");
        await CreatePostAsync(disc3.Id, user.Id, "Post D", isFirstPost: true);

        var result = await _service.GetSpacesAsync("hub-001");

        await Assert.That(result).IsNotNull();
        await Assert.That(result.Total).IsEqualTo(2);
        await Assert.That(result.Spaces.Count).IsEqualTo(2);

        var firstSpace = result.Spaces.First(s => s.Slug == "test-space");
        await Assert.That(firstSpace.Name).IsEqualTo("Test Space");
        await Assert.That(firstSpace.DiscussionCount).IsEqualTo(2);
        await Assert.That(firstSpace.PostCount).IsEqualTo(3);
        await Assert.That(firstSpace.IsActive).IsTrue();

        var secondSpace = result.Spaces.First(s => s.Slug == "space-two");
        await Assert.That(secondSpace.Name).IsEqualTo("Space Two");
        await Assert.That(secondSpace.DiscussionCount).IsEqualTo(1);
        await Assert.That(secondSpace.PostCount).IsEqualTo(1);
    }

    #endregion
}
