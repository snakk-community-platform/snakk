using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.Services;

namespace Snakk.Infrastructure.Tests.Services;

public class EntityHierarchyCacheServiceTests : IDisposable
{
    private readonly SnakkDbContext _context;
    private readonly ServiceProvider _serviceProvider;
    private readonly HybridCache _cache;
    private readonly EntityHierarchyCacheService _service;

    public EntityHierarchyCacheServiceTests()
    {
        var options = new DbContextOptionsBuilder<SnakkDbContext>()
            .UseInMemoryDatabase(databaseName: $"EntityHierarchyCacheTests_{Guid.NewGuid()}")
            .Options;
        _context = new SnakkDbContext(options);
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddHybridCache();
        _serviceProvider = serviceCollection.BuildServiceProvider();
        _cache = _serviceProvider.GetRequiredService<HybridCache>();
        _service = new EntityHierarchyCacheService(_context, _cache);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
        _serviceProvider.Dispose();
    }

    private async Task<(CommunityDatabaseEntity community, HubDatabaseEntity hub, SpaceDatabaseEntity space, DiscussionDatabaseEntity discussion)> SetupHierarchy(
        string commPid = "comm_hc", string hubPid = "hub_hc", string spacePid = "space_hc", string discPid = "disc_hc")
    {
        var community = new CommunityDatabaseEntity
        {
            PublicId = commPid,
            Name = "HC Community",
            Slug = "hc-community",
            CreatedAt = DateTime.UtcNow,
            VisibilityId = 1
        };
        _context.Communities.Add(community);
        await _context.SaveChangesAsync();

        var hub = new HubDatabaseEntity
        {
            PublicId = hubPid,
            Name = "HC Hub",
            Slug = "hc-hub",
            CommunityId = community.Id,
            CreatedAt = DateTime.UtcNow
        };
        _context.Hubs.Add(hub);
        await _context.SaveChangesAsync();

        var space = new SpaceDatabaseEntity
        {
            PublicId = spacePid,
            Name = "HC Space",
            Slug = "hc-space",
            HubId = hub.Id,
            CreatedAt = DateTime.UtcNow
        };
        _context.Spaces.Add(space);
        await _context.SaveChangesAsync();

        var discussion = new DiscussionDatabaseEntity
        {
            PublicId = discPid,
            Title = "HC Discussion",
            Slug = "hc-discussion",
            SpaceId = space.Id,
            HubId = hub.Id,
            CommunityId = community.Id,
            CreatedByUserId = 1,
            CreatedAt = DateTime.UtcNow
        };
        _context.Discussions.Add(discussion);
        await _context.SaveChangesAsync();

        return (community, hub, space, discussion);
    }

    #region GetDiscussionHierarchyAsync Tests

    [Test]
    public async Task GetDiscussionHierarchyAsync_CacheMiss_ReturnsHierarchyFromDb()
    {
        // Arrange
        var (community, hub, space, discussion) = await SetupHierarchy();

        // Act
        var result = await _service.GetDiscussionHierarchyAsync("disc_hc");

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.SpaceId).IsEqualTo(space.Id);
        await Assert.That(result.HubId).IsEqualTo(hub.Id);
        await Assert.That(result.CommunityId).IsEqualTo(community.Id);
    }

    [Test]
    public async Task GetDiscussionHierarchyAsync_CacheHit_ReturnsCachedValueAfterDbRemoval()
    {
        // Arrange
        var (_, _, space, discussion) = await SetupHierarchy("comm_hc2", "hub_hc2", "space_hc2", "disc_hc2");
        await _service.GetDiscussionHierarchyAsync("disc_hc2"); // populate cache

        // Remove from DB
        _context.Discussions.Remove(discussion);
        await _context.SaveChangesAsync();

        // Act — should still return from cache
        var result = await _service.GetDiscussionHierarchyAsync("disc_hc2");

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.SpaceId).IsEqualTo(space.Id);
    }

    [Test]
    public async Task GetDiscussionHierarchyAsync_UnknownId_ReturnsNull()
    {
        // Act
        var result = await _service.GetDiscussionHierarchyAsync("nonexistent_disc");

        // Assert
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task GetDiscussionHierarchyAsync_NullNotCached_SubsequentCallHitsDb()
    {
        // Arrange — first call returns null (entity absent)
        var result1 = await _service.GetDiscussionHierarchyAsync("disc_hc3");
        await Assert.That(result1).IsNull();

        // Add entity to DB after first null call
        var community = new CommunityDatabaseEntity
        {
            PublicId = "comm_hc3", Name = "C", Slug = "c", CreatedAt = DateTime.UtcNow, VisibilityId = 1
        };
        _context.Communities.Add(community);
        await _context.SaveChangesAsync();
        var hub = new HubDatabaseEntity
        {
            PublicId = "hub_hc3", Name = "H", Slug = "h", CommunityId = community.Id, CreatedAt = DateTime.UtcNow
        };
        _context.Hubs.Add(hub);
        await _context.SaveChangesAsync();
        var space = new SpaceDatabaseEntity
        {
            PublicId = "space_hc3", Name = "S", Slug = "s", HubId = hub.Id, CreatedAt = DateTime.UtcNow
        };
        _context.Spaces.Add(space);
        await _context.SaveChangesAsync();
        _context.Discussions.Add(new DiscussionDatabaseEntity
        {
            PublicId = "disc_hc3", Title = "T", Slug = "t",
            SpaceId = space.Id, HubId = hub.Id, CommunityId = community.Id,
            CreatedByUserId = 1, CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        // Act — second call should hit DB and return the entity
        var result2 = await _service.GetDiscussionHierarchyAsync("disc_hc3");

        // Assert
        await Assert.That(result2).IsNotNull();
    }

    #endregion

    #region GetSpaceHierarchyAsync Tests

    [Test]
    public async Task GetSpaceHierarchyAsync_CacheMiss_ReturnsHierarchyFromDb()
    {
        // Arrange
        var (community, hub, space, _) = await SetupHierarchy("comm_sh", "hub_sh", "space_sh", "disc_sh");

        // Act
        var result = await _service.GetSpaceHierarchyAsync("space_sh");

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Id).IsEqualTo(space.Id);
        await Assert.That(result.HubId).IsEqualTo(hub.Id);
        await Assert.That(result.CommunityId).IsEqualTo(community.Id);
    }

    [Test]
    public async Task GetSpaceHierarchyAsync_CacheHit_ReturnsCachedValueAfterDbRemoval()
    {
        // Arrange
        var (_, _, space, discussion) = await SetupHierarchy("comm_sh2", "hub_sh2", "space_sh2", "disc_sh2");
        await _service.GetSpaceHierarchyAsync("space_sh2"); // populate cache

        // Remove dependents first (discussion → space)
        _context.Discussions.Remove(discussion);
        await _context.SaveChangesAsync();
        _context.Spaces.Remove(space);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetSpaceHierarchyAsync("space_sh2");

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Id).IsEqualTo(space.Id);
    }

    [Test]
    public async Task GetSpaceHierarchyAsync_UnknownId_ReturnsNull()
    {
        var result = await _service.GetSpaceHierarchyAsync("nonexistent_space");
        await Assert.That(result).IsNull();
    }

    #endregion

    #region GetHubHierarchyAsync Tests

    [Test]
    public async Task GetHubHierarchyAsync_CacheMiss_ReturnsHierarchyFromDb()
    {
        // Arrange
        var (community, hub, _, _) = await SetupHierarchy("comm_hh", "hub_hh", "space_hh", "disc_hh");

        // Act
        var result = await _service.GetHubHierarchyAsync("hub_hh");

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Id).IsEqualTo(hub.Id);
        await Assert.That(result.CommunityId).IsEqualTo(community.Id);
    }

    [Test]
    public async Task GetHubHierarchyAsync_CacheHit_ReturnsCachedValueAfterDbRemoval()
    {
        // Arrange
        var (_, hub, space, discussion) = await SetupHierarchy("comm_hh2", "hub_hh2", "space_hh2", "disc_hh2");
        await _service.GetHubHierarchyAsync("hub_hh2"); // populate cache

        // Remove dependents first (discussion → space → hub)
        _context.Discussions.Remove(discussion);
        await _context.SaveChangesAsync();
        _context.Spaces.Remove(space);
        await _context.SaveChangesAsync();
        _context.Hubs.Remove(hub);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetHubHierarchyAsync("hub_hh2");

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Id).IsEqualTo(hub.Id);
    }

    [Test]
    public async Task GetHubHierarchyAsync_UnknownId_ReturnsNull()
    {
        var result = await _service.GetHubHierarchyAsync("nonexistent_hub");
        await Assert.That(result).IsNull();
    }

    #endregion

    #region GetCommunityIdAsync Tests

    [Test]
    public async Task GetCommunityIdAsync_CacheMiss_ReturnsIdFromDb()
    {
        // Arrange
        var (community, _, _, _) = await SetupHierarchy("comm_ci", "hub_ci", "space_ci", "disc_ci");

        // Act
        var result = await _service.GetCommunityIdAsync("comm_ci");

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value).IsEqualTo(community.Id);
    }

    [Test]
    public async Task GetCommunityIdAsync_CacheHit_ReturnsCachedValueAfterDbRemoval()
    {
        // Arrange
        var (community, hub, space, discussion) = await SetupHierarchy("comm_ci2", "hub_ci2", "space_ci2", "disc_ci2");
        await _service.GetCommunityIdAsync("comm_ci2"); // populate cache

        // Remove dependents first (discussion → space → hub → community)
        _context.Discussions.Remove(discussion);
        await _context.SaveChangesAsync();
        _context.Spaces.Remove(space);
        await _context.SaveChangesAsync();
        _context.Hubs.Remove(hub);
        await _context.SaveChangesAsync();
        _context.Communities.Remove(community);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetCommunityIdAsync("comm_ci2");

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value).IsEqualTo(community.Id);
    }

    [Test]
    public async Task GetCommunityIdAsync_UnknownId_ReturnsNull()
    {
        var result = await _service.GetCommunityIdAsync("nonexistent_community");
        await Assert.That(result).IsNull();
    }

    #endregion
}
