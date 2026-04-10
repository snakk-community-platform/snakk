using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.Services;
using Snakk.Application.DTOs.Management;
using Snakk.Shared.Enums;

namespace Snakk.Infrastructure.Tests.Services;

public class CommunityManagementServiceTests : IDisposable
{
    private readonly SnakkDbContext _context;
    private readonly CommunityManagementService _service;

    public CommunityManagementServiceTests()
    {
        var options = new DbContextOptionsBuilder<SnakkDbContext>()
            .UseInMemoryDatabase(databaseName: $"CommunityManagementServiceTests_{Guid.NewGuid()}")
            .Options;
        _context = new SnakkDbContext(options);
        _service = new CommunityManagementService(_context);
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
            CreatedAt = DateTime.UtcNow
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

    private async Task<PostDatabaseEntity> CreatePostAsync(int discussionId, int userId, string content = "Test post content")
    {
        var post = new PostDatabaseEntity
        {
            PublicId = $"post-{Guid.NewGuid():N}",
            DiscussionId = discussionId,
            CreatedByUserId = userId,
            Content = content,
            CreatedAt = DateTime.UtcNow
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
    public async Task GetOverviewAsync_ReturnsCorrectCounts()
    {
        var (user, community, hub, space) = await CreateHierarchyAsync();

        // Add a second hub and space
        var hub2 = new HubDatabaseEntity { PublicId = "hub-002", CommunityId = community.Id, Name = "Hub 2", Slug = "hub-2", CreatedAt = DateTime.UtcNow };
        _context.Hubs.Add(hub2);
        community.HubCount++;
        await _context.SaveChangesAsync();

        var space2 = new SpaceDatabaseEntity { PublicId = "space-002", HubId = hub2.Id, Name = "Space 2", Slug = "space-2", CreatedAt = DateTime.UtcNow };
        _context.Spaces.Add(space2);
        hub2.SpaceCount++;
        community.SpaceCount++;
        await _context.SaveChangesAsync();

        // Create discussions and posts
        var disc1 = await CreateDiscussionAsync(space.Id, user.Id, "Discussion 1");
        var disc2 = await CreateDiscussionAsync(space.Id, user.Id, "Discussion 2");
        var disc3 = await CreateDiscussionAsync(space2.Id, user.Id, "Discussion 3");

        await CreatePostAsync(disc1.Id, user.Id, "Post 1");
        await CreatePostAsync(disc1.Id, user.Id, "Post 2");
        await CreatePostAsync(disc2.Id, user.Id, "Post 3");

        var result = await _service.GetOverviewAsync("comm-001");

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.TotalHubs).IsEqualTo(2);
        await Assert.That(result.TotalSpaces).IsEqualTo(2);
        await Assert.That(result.TotalDiscussions).IsEqualTo(3);
        await Assert.That(result.TotalPosts).IsEqualTo(3);
        await Assert.That(result.Name).IsEqualTo("Test Community");
        await Assert.That(result.Slug).IsEqualTo("test-community");
    }

    [Test]
    public async Task GetOverviewAsync_WhenCommunityNotFound_ReturnsNull()
    {
        var result = await _service.GetOverviewAsync("nonexistent-community");

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task GetOverviewAsync_ReturnsPostActivityStats()
    {
        var (user, community, hub, space) = await CreateHierarchyAsync();

        var disc = await CreateDiscussionAsync(space.Id, user.Id);

        // Post created now (should count for today and this week)
        await CreatePostAsync(disc.Id, user.Id, "Recent post");

        var result = await _service.GetOverviewAsync("comm-001");

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.PostsToday).IsEqualTo(1);
        await Assert.That(result.PostsThisWeek).IsEqualTo(1);
    }

    #endregion

    #region GetSettingsAsync Tests

    [Test]
    public async Task GetSettingsAsync_ReturnsNameAndDescription()
    {
        var community = new CommunityDatabaseEntity
        {
            PublicId = "comm-settings",
            Name = "Settings Community",
            Slug = "settings-community",
            Description = "A community for settings testing",
            CreatedAt = DateTime.UtcNow,
            VisibilityId = 1
        };
        _context.Communities.Add(community);
        await _context.SaveChangesAsync();

        var result = await _service.GetSettingsAsync("comm-settings");

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Name).IsEqualTo("Settings Community");
        await Assert.That(result.Description).IsEqualTo("A community for settings testing");
        await Assert.That(result.Slug).IsEqualTo("settings-community");
    }

    [Test]
    public async Task GetSettingsAsync_ReturnsAdminAndModUserIds()
    {
        var (user, community, _, _) = await CreateHierarchyAsync();

        var adminUser = new UserDatabaseEntity { PublicId = "admin-001", DisplayName = "Admin User", Email = "admin@test.com", CreatedAt = DateTime.UtcNow };
        var modUser = new UserDatabaseEntity { PublicId = "mod-001", DisplayName = "Mod User", Email = "mod@test.com", CreatedAt = DateTime.UtcNow };
        _context.Users.AddRange(adminUser, modUser);
        await _context.SaveChangesAsync();

        _context.UserRoles.Add(new UserRoleDatabaseEntity
        {
            PublicId = Guid.NewGuid().ToString(),
            UserId = adminUser.Id,
            RoleId = (int)UserRoleTypeEnum.CommunityAdmin,
            CommunityId = community.Id,
            AssignedAt = DateTime.UtcNow,
            AssignedByUserId = user.Id
        });
        _context.UserRoles.Add(new UserRoleDatabaseEntity
        {
            PublicId = Guid.NewGuid().ToString(),
            UserId = modUser.Id,
            RoleId = (int)UserRoleTypeEnum.CommunityMod,
            CommunityId = community.Id,
            AssignedAt = DateTime.UtcNow,
            AssignedByUserId = user.Id
        });
        await _context.SaveChangesAsync();

        var result = await _service.GetSettingsAsync("comm-001");

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.AdminUserIds).Contains("admin-001");
        await Assert.That(result.ModeratorUserIds).Contains("mod-001");
    }

    [Test]
    public async Task GetSettingsAsync_WhenCommunityNotFound_ReturnsNull()
    {
        var result = await _service.GetSettingsAsync("nonexistent-slug");

        await Assert.That(result).IsNull();
    }

    #endregion

    #region UpdateSettingsAsync Tests

    [Test]
    public async Task UpdateSettingsAsync_UpdatesNameAndDescription()
    {
        var community = new CommunityDatabaseEntity
        {
            PublicId = "comm-update",
            Name = "Original Name",
            Slug = "update-community",
            Description = "Original description",
            CreatedAt = DateTime.UtcNow,
            VisibilityId = 1
        };
        _context.Communities.Add(community);
        await _context.SaveChangesAsync();

        var request = new UpdateCommunitySettingsRequest
        {
            Name = "Updated Name",
            Description = "Updated description"
        };

        var result = await _service.UpdateSettingsAsync("comm-update", request);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Name).IsEqualTo("Updated Name");
        await Assert.That(result.Description).IsEqualTo("Updated description");

        // Verify persistence
        var persisted = await _context.Communities.FirstAsync(c => c.Slug == "update-community");
        await Assert.That(persisted.Name).IsEqualTo("Updated Name");
        await Assert.That(persisted.Description).IsEqualTo("Updated description");
    }

    [Test]
    public async Task UpdateSettingsAsync_WhenCommunityNotFound_ReturnsNull()
    {
        var request = new UpdateCommunitySettingsRequest
        {
            Name = "Does Not Matter",
            Description = "Irrelevant"
        };

        var result = await _service.UpdateSettingsAsync("nonexistent-slug", request);

        await Assert.That(result).IsNull();
    }

    #endregion

    #region GetCommunitySpacesAsync Tests

    [Test]
    public async Task GetCommunitySpacesAsync_ReturnsSpacesWithCounts()
    {
        var (user, community, hub, space) = await CreateHierarchyAsync();

        var disc1 = await CreateDiscussionAsync(space.Id, user.Id, "Disc 1");
        var disc2 = await CreateDiscussionAsync(space.Id, user.Id, "Disc 2");
        await CreatePostAsync(disc1.Id, user.Id, "Post A");
        await CreatePostAsync(disc1.Id, user.Id, "Post B");
        await CreatePostAsync(disc2.Id, user.Id, "Post C");

        var result = await _service.GetCommunitySpacesAsync("comm-001");

        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].Name).IsEqualTo("Test Space");
        await Assert.That(result[0].Slug).IsEqualTo("test-space");
        await Assert.That(result[0].DiscussionCount).IsEqualTo(2);
        await Assert.That(result[0].PostCount).IsEqualTo(3);
        await Assert.That(result[0].IsActive).IsTrue();
    }

    [Test]
    public async Task GetCommunitySpacesAsync_WhenNoCommunity_ReturnsEmptyList()
    {
        var result = await _service.GetCommunitySpacesAsync("nonexistent-community");

        await Assert.That(result).IsNotNull();
        await Assert.That(result.Count).IsEqualTo(0);
    }

    #endregion

    #region GetMembersAsync Tests

    [Test]
    public async Task GetMembersAsync_ReturnsEmptyList()
    {
        // GetMembersAsync is a TODO stub that returns empty results
        var result = await _service.GetMembersAsync("any-slug", page: 1, pageSize: 50);

        await Assert.That(result).IsNotNull();
        await Assert.That(result.Members.Count).IsEqualTo(0);
        await Assert.That(result.Total).IsEqualTo(0);
        await Assert.That(result.Page).IsEqualTo(1);
        await Assert.That(result.PageSize).IsEqualTo(50);
    }

    #endregion
}
