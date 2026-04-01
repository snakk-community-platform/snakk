using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.Services;
using Snakk.Application.DTOs.Management;
using Snakk.Shared.Enums;

namespace Snakk.Infrastructure.Tests.Services;

public class SpaceManagementServiceTests : IDisposable
{
    private readonly SnakkDbContext _context;
    private readonly SpaceManagementService _service;

    public SpaceManagementServiceTests()
    {
        var options = new DbContextOptionsBuilder<SnakkDbContext>()
            .UseInMemoryDatabase(databaseName: $"SpaceManagementServiceTests_{Guid.NewGuid()}")
            .Options;
        _context = new SnakkDbContext(options);
        _service = new SpaceManagementService(_context, Substitute.For<ILogger<SpaceManagementService>>());
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
    public async Task GetOverviewAsync_ReturnsSpaceStatsWithDiscussionPostAndFollowerCounts()
    {
        var (user, community, hub, space) = await CreateHierarchyAsync();

        // Create discussions and posts
        var disc1 = await CreateDiscussionAsync(space.Id, user.Id, "Discussion 1");
        var disc2 = await CreateDiscussionAsync(space.Id, user.Id, "Discussion 2");
        await CreatePostAsync(disc1.Id, user.Id, "Post 1", isFirstPost: true);
        await CreatePostAsync(disc1.Id, user.Id, "Post 2");
        await CreatePostAsync(disc2.Id, user.Id, "Post 3", isFirstPost: true);

        // Create a follower for the space
        var follower = new UserDatabaseEntity { PublicId = "follower-001", DisplayName = "Follower", Email = "follower@test.com", CreatedAt = DateTime.UtcNow };
        _context.Users.Add(follower);
        await _context.SaveChangesAsync();

        _context.Follows.Add(new FollowDatabaseEntity
        {
            PublicId = $"follow-{Guid.NewGuid():N}",
            UserId = follower.Id,
            SpaceId = space.Id,
            TargetTypeId = (int)FollowTargetTypeEnum.Space,
            LevelId = 1,
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var result = await _service.GetOverviewAsync("space-001");

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Name).IsEqualTo("Test Space");
        await Assert.That(result.Slug).IsEqualTo("test-space");
        await Assert.That(result.CommunitySlug).IsEqualTo("test-community");
        await Assert.That(result.CommunityName).IsEqualTo("Test Community");
        await Assert.That(result.HubSlug).IsEqualTo("test-hub");
        await Assert.That(result.HubName).IsEqualTo("Test Hub");
        await Assert.That(result.TotalDiscussions).IsEqualTo(2);
        await Assert.That(result.TotalPosts).IsEqualTo(3);
        await Assert.That(result.Followers).IsEqualTo(1);
    }

    [Test]
    public async Task GetOverviewAsync_WhenSpaceNotFound_ReturnsNull()
    {
        // Create community and hub but no matching space
        var community = new CommunityDatabaseEntity { PublicId = "comm-empty", Name = "Empty Community", Slug = "empty-community", CreatedAt = DateTime.UtcNow, VisibilityId = 1 };
        _context.Communities.Add(community);
        await _context.SaveChangesAsync();

        var hub = new HubDatabaseEntity { PublicId = "hub-empty", CommunityId = community.Id, Name = "Empty Hub", Slug = "empty-hub", CreatedAt = DateTime.UtcNow };
        _context.Hubs.Add(hub);
        await _context.SaveChangesAsync();

        var result = await _service.GetOverviewAsync("nonexistent-space");

        await Assert.That(result).IsNull();
    }

    #endregion

    #region GetSettingsAsync Tests

    [Test]
    public async Task GetSettingsAsync_ReturnsSpaceNameAndDescription()
    {
        var community = new CommunityDatabaseEntity { PublicId = "comm-settings", Name = "Settings Community", Slug = "settings-community", CreatedAt = DateTime.UtcNow, VisibilityId = 1 };
        _context.Communities.Add(community);
        await _context.SaveChangesAsync();

        var hub = new HubDatabaseEntity { PublicId = "hub-settings", CommunityId = community.Id, Name = "Settings Hub", Slug = "settings-hub", CreatedAt = DateTime.UtcNow };
        _context.Hubs.Add(hub);
        await _context.SaveChangesAsync();

        var space = new SpaceDatabaseEntity
        {
            PublicId = "space-settings",
            HubId = hub.Id,
            Name = "Settings Space",
            Slug = "settings-space",
            Description = "A space for settings testing",
            CreatedAt = DateTime.UtcNow
        };
        _context.Spaces.Add(space);
        await _context.SaveChangesAsync();

        var result = await _service.GetSettingsAsync("space-settings");

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Name).IsEqualTo("Settings Space");
        await Assert.That(result.Description).IsEqualTo("A space for settings testing");
        await Assert.That(result.Slug).IsEqualTo("settings-space");
    }

    #endregion

    #region UpdateSettingsAsync Tests

    [Test]
    public async Task UpdateSettingsAsync_UpdatesNameAndDescription()
    {
        var community = new CommunityDatabaseEntity { PublicId = "comm-update", Name = "Update Community", Slug = "update-community", CreatedAt = DateTime.UtcNow, VisibilityId = 1 };
        _context.Communities.Add(community);
        await _context.SaveChangesAsync();

        var hub = new HubDatabaseEntity { PublicId = "hub-update", CommunityId = community.Id, Name = "Update Hub", Slug = "update-hub", CreatedAt = DateTime.UtcNow };
        _context.Hubs.Add(hub);
        await _context.SaveChangesAsync();

        var space = new SpaceDatabaseEntity
        {
            PublicId = "space-update",
            HubId = hub.Id,
            Name = "Original Space Name",
            Slug = "update-space",
            Description = "Original description",
            CreatedAt = DateTime.UtcNow
        };
        _context.Spaces.Add(space);
        await _context.SaveChangesAsync();

        var request = new UpdateSpaceSettingsRequest
        {
            Name = "Updated Space Name",
            Description = "Updated space description"
        };

        var result = await _service.UpdateSettingsAsync("space-update", request);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Name).IsEqualTo("Updated Space Name");
        await Assert.That(result.Description).IsEqualTo("Updated space description");

        // Verify persistence
        var persisted = await _context.Spaces.FirstAsync(s => s.Slug == "update-space");
        await Assert.That(persisted.Name).IsEqualTo("Updated Space Name");
        await Assert.That(persisted.Description).IsEqualTo("Updated space description");
    }

    #endregion

    #region GetModerationDataAsync Tests

    [Test]
    public async Task GetModerationDataAsync_WhenSpaceNotFound_ReturnsEmptyModerationData()
    {
        // No data seeded at all - spaceId will be 0
        var result = await _service.GetModerationDataAsync("nonexistent-space");

        await Assert.That(result).IsNotNull();
        await Assert.That(result.PendingReports.Count).IsEqualTo(0);
        await Assert.That(result.RecentActions.Count).IsEqualTo(0);
        await Assert.That(result.Stats.TotalReports).IsEqualTo(0);
        await Assert.That(result.Stats.PendingReports).IsEqualTo(0);
        await Assert.That(result.Stats.ResolvedReports).IsEqualTo(0);
        await Assert.That(result.Stats.DismissedReports).IsEqualTo(0);
    }

    #endregion
}
