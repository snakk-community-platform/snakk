using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.Services;
using Snakk.Shared.Enums;

namespace Snakk.Infrastructure.Tests.Services;

public class AdminContentServiceTests : IDisposable
{
    private readonly SnakkDbContext _context;
    private readonly AdminContentService _service;
    private readonly ServiceProvider _cacheServiceProvider;
    private readonly ISecurityService _securityService;

    public AdminContentServiceTests()
    {
        var options = new DbContextOptionsBuilder<SnakkDbContext>()
            .UseInMemoryDatabase(databaseName: $"AdminContentServiceTests_{Guid.NewGuid()}")
            .Options;
        _context = new SnakkDbContext(options);
        var services = new ServiceCollection();
        services.AddHybridCache();
        _cacheServiceProvider = services.BuildServiceProvider();
        var cache = _cacheServiceProvider.GetRequiredService<HybridCache>();
        _securityService = Substitute.For<ISecurityService>();

        _securityService.LogAuditAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<bool>(),
                Arg.Any<AuditLogSeverityEnum>())
            .Returns(Task.CompletedTask);

        _service = new AdminContentService(
            _context,
            cache,
            _securityService,
            Substitute.For<ILogger<AdminContentService>>());
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
        _cacheServiceProvider.Dispose();
    }

    #region Helpers

    private async Task SeedFullHierarchyAsync()
    {
        var user = new UserDatabaseEntity
        {
            PublicId = "admin-001",
            DisplayName = "Admin",
            Email = "admin@test.com",
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);

        var community = new CommunityDatabaseEntity
        {
            PublicId = "comm-001",
            Name = "Test Community",
            Slug = "test-community",
            CreatedAt = DateTime.UtcNow,
            VisibilityId = 1
        };
        _context.Communities.Add(community);
        await _context.SaveChangesAsync();

        var hub = new HubDatabaseEntity
        {
            PublicId = "hub-001",
            CommunityId = community.Id,
            Name = "Test Hub",
            Slug = "test-hub",
            CreatedAt = DateTime.UtcNow
        };
        _context.Hubs.Add(hub);
        await _context.SaveChangesAsync();

        var space = new SpaceDatabaseEntity
        {
            PublicId = "space-001",
            HubId = hub.Id,
            Name = "Test Space",
            Slug = "test-space",
            CreatedAt = DateTime.UtcNow
        };
        _context.Spaces.Add(space);
        await _context.SaveChangesAsync();

        var discussion = new DiscussionDatabaseEntity
        {
            PublicId = "disc-001",
            SpaceId = space.Id,
            CreatedByUserId = user.Id,
            Title = "Test Discussion",
            Slug = "test-discussion",
            CreatedAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow
        };
        _context.Discussions.Add(discussion);
        await _context.SaveChangesAsync();

        var post = new PostDatabaseEntity
        {
            PublicId = "post-001",
            DiscussionId = discussion.Id,
            CreatedByUserId = user.Id,
            Content = "Test post content",
            CreatedAt = DateTime.UtcNow,
            IsFirstPost = true
        };
        _context.Posts.Add(post);
        await _context.SaveChangesAsync();
    }

    #endregion

    #region GetContentOverviewAsync

    [Test]
    public async Task GetContentOverview_ReturnsCounts()
    {
        await SeedFullHierarchyAsync();

        var overview = await _service.GetContentOverviewAsync();

        await Assert.That(overview.TotalCommunities).IsEqualTo(1);
        await Assert.That(overview.TotalHubs).IsEqualTo(1);
        await Assert.That(overview.TotalSpaces).IsEqualTo(1);
        await Assert.That(overview.TotalDiscussions).IsEqualTo(1);
        await Assert.That(overview.TotalPosts).IsEqualTo(1);
    }

    #endregion

    #region GetCommunitiesAsync

    [Test]
    public async Task GetCommunities_ReturnsPaginated()
    {
        await SeedFullHierarchyAsync();

        var result = await _service.GetCommunitiesAsync(1, 10, null);

        await Assert.That(result.Items).Count().IsEqualTo(1);
        await Assert.That(result.Total).IsEqualTo(1);
        await Assert.That(result.Page).IsEqualTo(1);
        await Assert.That(result.PageSize).IsEqualTo(10);
        await Assert.That(result.Items[0].Name).IsEqualTo("Test Community");
        await Assert.That(result.Items[0].Slug).IsEqualTo("test-community");
        await Assert.That(result.Items[0].MemberCount).IsEqualTo(0);
    }

    [Test]
    public async Task GetCommunities_WithSearch_FiltersResults()
    {
        await SeedFullHierarchyAsync();

        // Add a second community that should NOT match
        _context.Communities.Add(new CommunityDatabaseEntity
        {
            PublicId = "comm-002",
            Name = "Other Forum",
            Slug = "other-forum",
            CreatedAt = DateTime.UtcNow,
            VisibilityId = 1
        });
        await _context.SaveChangesAsync();

        var result = await _service.GetCommunitiesAsync(1, 10, "Test");

        await Assert.That(result.Items).Count().IsEqualTo(1);
        await Assert.That(result.Total).IsEqualTo(1);
        await Assert.That(result.Items[0].Name).IsEqualTo("Test Community");
    }

    #endregion

    #region GetHubsAsync

    [Test]
    public async Task GetHubs_ReturnsPaginated()
    {
        await SeedFullHierarchyAsync();

        var result = await _service.GetHubsAsync(1, 10, null, null);

        await Assert.That(result.Items).Count().IsEqualTo(1);
        await Assert.That(result.Total).IsEqualTo(1);
        await Assert.That(result.Page).IsEqualTo(1);
        await Assert.That(result.PageSize).IsEqualTo(10);
        await Assert.That(result.Items[0].Name).IsEqualTo("Test Hub");
        await Assert.That(result.Items[0].Slug).IsEqualTo("test-hub");
        await Assert.That(result.Items[0].CommunitySlug).IsEqualTo("test-community");
        await Assert.That(result.Items[0].CommunityName).IsEqualTo("Test Community");
    }

    #endregion

    #region GetSpacesAsync

    [Test]
    public async Task GetSpaces_ReturnsPaginated()
    {
        await SeedFullHierarchyAsync();

        var result = await _service.GetSpacesAsync(1, 10, null, null);

        await Assert.That(result.Items).Count().IsEqualTo(1);
        await Assert.That(result.Total).IsEqualTo(1);
        await Assert.That(result.Page).IsEqualTo(1);
        await Assert.That(result.PageSize).IsEqualTo(10);
        await Assert.That(result.Items[0].Name).IsEqualTo("Test Space");
        await Assert.That(result.Items[0].Slug).IsEqualTo("test-space");
        await Assert.That(result.Items[0].HubSlug).IsEqualTo("test-hub");
        await Assert.That(result.Items[0].HubName).IsEqualTo("Test Hub");
        await Assert.That(result.Items[0].CommunitySlug).IsEqualTo("test-community");
    }

    #endregion

    #region GetDiscussionsAsync

    [Test]
    public async Task GetDiscussions_ReturnsPaginated()
    {
        await SeedFullHierarchyAsync();

        var result = await _service.GetDiscussionsAsync(1, 10, null, null, null, null);

        await Assert.That(result.Items).Count().IsEqualTo(1);
        await Assert.That(result.Total).IsEqualTo(1);
        await Assert.That(result.Page).IsEqualTo(1);
        await Assert.That(result.PageSize).IsEqualTo(10);
        await Assert.That(result.Items[0].Title).IsEqualTo("Test Discussion");
        await Assert.That(result.Items[0].Slug).IsEqualTo("test-discussion");
        await Assert.That(result.Items[0].AuthorDisplayName).IsEqualTo("Admin");
        await Assert.That(result.Items[0].SpaceSlug).IsEqualTo("test-space");
        await Assert.That(result.Items[0].SpaceName).IsEqualTo("Test Space");
        await Assert.That(result.Items[0].IsPinned).IsFalse();
        await Assert.That(result.Items[0].IsLocked).IsFalse();
    }

    #endregion

    #region PinDiscussionAsync

    [Test]
    public async Task PinDiscussion_SetsIsPinnedTrue()
    {
        await SeedFullHierarchyAsync();

        var result = await _service.PinDiscussionAsync("test-discussion", "admin-001");

        await Assert.That(result).IsTrue();

        var discussion = await _context.Discussions.FirstAsync(d => d.Slug == "test-discussion");
        await Assert.That(discussion.IsPinned).IsTrue();
    }

    [Test]
    public async Task PinDiscussion_WhenNotFound_ReturnsFalse()
    {
        var result = await _service.PinDiscussionAsync("nonexistent-slug", "admin-001");

        await Assert.That(result).IsFalse();
    }

    #endregion

    #region LockDiscussionAsync

    [Test]
    public async Task LockDiscussion_SetsIsLockedTrue()
    {
        await SeedFullHierarchyAsync();

        var result = await _service.LockDiscussionAsync("test-discussion", "admin-001");

        await Assert.That(result).IsTrue();

        var discussion = await _context.Discussions.FirstAsync(d => d.Slug == "test-discussion");
        await Assert.That(discussion.IsLocked).IsTrue();
    }

    #endregion

    #region DeleteDiscussionAsync

    [Test]
    public async Task DeleteDiscussion_RemovesFromDatabase()
    {
        // Seed a discussion without posts to avoid cascade delete issues with in-memory DB
        var user = new UserDatabaseEntity
        {
            PublicId = "admin-del",
            DisplayName = "Admin",
            Email = "admin-del@test.com",
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);

        var community = new CommunityDatabaseEntity
        {
            PublicId = "comm-del",
            Name = "Del Community",
            Slug = "del-community",
            CreatedAt = DateTime.UtcNow,
            VisibilityId = 1
        };
        _context.Communities.Add(community);
        await _context.SaveChangesAsync();

        var hub = new HubDatabaseEntity
        {
            PublicId = "hub-del",
            CommunityId = community.Id,
            Name = "Del Hub",
            Slug = "del-hub",
            CreatedAt = DateTime.UtcNow
        };
        _context.Hubs.Add(hub);
        await _context.SaveChangesAsync();

        var space = new SpaceDatabaseEntity
        {
            PublicId = "space-del",
            HubId = hub.Id,
            Name = "Del Space",
            Slug = "del-space",
            CreatedAt = DateTime.UtcNow
        };
        _context.Spaces.Add(space);
        await _context.SaveChangesAsync();

        var discussion = new DiscussionDatabaseEntity
        {
            PublicId = "disc-del",
            SpaceId = space.Id,
            CreatedByUserId = user.Id,
            Title = "Delete Me",
            Slug = "delete-me",
            CreatedAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow
        };
        _context.Discussions.Add(discussion);
        await _context.SaveChangesAsync();

        var result = await _service.DeleteDiscussionAsync("delete-me", "admin-del");

        await Assert.That(result).IsTrue();

        var deleted = await _context.Discussions.FirstOrDefaultAsync(d => d.Slug == "delete-me");
        await Assert.That(deleted).IsNull();
    }

    #endregion
}
