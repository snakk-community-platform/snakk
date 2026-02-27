using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.Database.Repositories;
using Snakk.Infrastructure.Tests.Helpers;
using Snakk.Shared.Enums;

namespace Snakk.Infrastructure.Tests.Repositories;

public class CommunityDatabaseRepositoryTests : IDisposable
{
    private readonly SqliteTestDatabase _db;
    private readonly TestDataBuilder _builder;
    private readonly CommunityDatabaseRepository _repository;

    public CommunityDatabaseRepositoryTests()
    {
        _db = new SqliteTestDatabase();
        _builder = new TestDataBuilder(_db.Context);
        _repository = new CommunityDatabaseRepository(_db.Context);
    }

    public void Dispose() => _db.Dispose();

    #region GetByPublicIdAsync Tests

    [Test]
    public async Task GetByPublicIdAsync_ExistingCommunity_ReturnsEntity()
    {
        var community = await _builder.CreateCommunityAsync("Test Community", "test-community");

        var result = await _repository.GetByPublicIdAsync(community.PublicId);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.PublicId).IsEqualTo(community.PublicId);
        await Assert.That(result.Name).IsEqualTo("Test Community");
        await Assert.That(result.Slug).IsEqualTo("test-community");
    }

    [Test]
    public async Task GetByPublicIdAsync_NonExistent_ReturnsNull()
    {
        var result = await _repository.GetByPublicIdAsync("comm_nonexistent");

        await Assert.That(result).IsNull();
    }

    #endregion

    #region GetBySlugAsync Tests

    [Test]
    public async Task GetBySlugAsync_ExistingCommunity_ReturnsEntity()
    {
        var community = await _builder.CreateCommunityAsync("Slug Community", "slug-community");

        var result = await _repository.GetBySlugAsync("slug-community");

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Slug).IsEqualTo("slug-community");
        await Assert.That(result.Name).IsEqualTo("Slug Community");
    }

    [Test]
    public async Task GetBySlugAsync_NonExistent_ReturnsNull()
    {
        var result = await _repository.GetBySlugAsync("nonexistent-slug");

        await Assert.That(result).IsNull();
    }

    #endregion

    #region GetByDomainAsync Tests

    [Test]
    public async Task GetByDomainAsync_VerifiedDomain_ReturnsCommunity()
    {
        var community = await _builder.CreateCommunityAsync("Domain Community");

        var domain = new CommunityDomainDatabaseEntity
        {
            PublicId = $"cd_{Guid.NewGuid():N}",
            CommunityId = community.Id,
            Domain = "forum.example.com",
            IsPrimary = true,
            IsVerified = true,
            CreatedAt = DateTime.UtcNow
        };
        _db.Context.CommunityDomains.Add(domain);
        await _db.Context.SaveChangesAsync();

        var result = await _repository.GetByDomainAsync("forum.example.com");

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Id).IsEqualTo(community.Id);
        await Assert.That(result.Name).IsEqualTo("Domain Community");
    }

    [Test]
    public async Task GetByDomainAsync_UnverifiedDomain_ReturnsNull()
    {
        var community = await _builder.CreateCommunityAsync("Unverified Community");

        var domain = new CommunityDomainDatabaseEntity
        {
            PublicId = $"cd_{Guid.NewGuid():N}",
            CommunityId = community.Id,
            Domain = "unverified.example.com",
            IsPrimary = true,
            IsVerified = false,
            CreatedAt = DateTime.UtcNow
        };
        _db.Context.CommunityDomains.Add(domain);
        await _db.Context.SaveChangesAsync();

        var result = await _repository.GetByDomainAsync("unverified.example.com");

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task GetByDomainAsync_NonExistentDomain_ReturnsNull()
    {
        var result = await _repository.GetByDomainAsync("nonexistent.example.com");

        await Assert.That(result).IsNull();
    }

    #endregion

    #region GetPublicListedAsync Tests

    [Test]
    public async Task GetPublicListedAsync_MixedVisibility_ReturnsOnlyPublicListed()
    {
        // Create PublicListed communities (default from CreateCommunityAsync)
        var publicCommunity1 = await _builder.CreateCommunityAsync("Alpha Public");
        var publicCommunity2 = await _builder.CreateCommunityAsync("Beta Public");

        // Create an unlisted community by changing VisibilityId after creation
        var unlistedCommunity = await _builder.CreateCommunityAsync("Unlisted Community");
        unlistedCommunity.VisibilityId = (int)CommunityVisibilityEnum.PublicUnlisted;
        await _db.Context.SaveChangesAsync();

        var result = await _repository.GetPublicListedAsync(0, 10);

        await Assert.That(result.Items.Count()).IsEqualTo(2);
        await Assert.That(result.HasMoreItems).IsFalse();

        var items = result.Items.ToList();
        // Ordered by Name
        await Assert.That(items[0].Name).IsEqualTo("Alpha Public");
        await Assert.That(items[1].Name).IsEqualTo("Beta Public");
    }

    [Test]
    public async Task GetPublicListedAsync_Pagination_ReturnsCorrectPage()
    {
        // Create 3 PublicListed communities
        await _builder.CreateCommunityAsync("AAA Community");
        await _builder.CreateCommunityAsync("BBB Community");
        await _builder.CreateCommunityAsync("CCC Community");

        // Request pageSize=2 (should return 2 items with HasMoreItems=true)
        var result = await _repository.GetPublicListedAsync(0, 2);

        await Assert.That(result.Items.Count()).IsEqualTo(2);
        await Assert.That(result.HasMoreItems).IsTrue();
        await Assert.That(result.PageSize).IsEqualTo(2);

        var items = result.Items.ToList();
        await Assert.That(items[0].Name).IsEqualTo("AAA Community");
        await Assert.That(items[1].Name).IsEqualTo("BBB Community");
    }

    [Test]
    public async Task GetPublicListedAsync_PaginationOffset_ReturnsRemainingItems()
    {
        await _builder.CreateCommunityAsync("AAA Community");
        await _builder.CreateCommunityAsync("BBB Community");
        await _builder.CreateCommunityAsync("CCC Community");

        // Request offset=2, pageSize=2 (should return 1 item with HasMoreItems=false)
        var result = await _repository.GetPublicListedAsync(2, 2);

        await Assert.That(result.Items.Count()).IsEqualTo(1);
        await Assert.That(result.HasMoreItems).IsFalse();

        var items = result.Items.ToList();
        await Assert.That(items[0].Name).IsEqualTo("CCC Community");
    }

    [Test]
    public async Task GetPublicListedAsync_Empty_ReturnsEmptyItems()
    {
        var result = await _repository.GetPublicListedAsync(0, 10);

        await Assert.That(result.Items.Count()).IsEqualTo(0);
        await Assert.That(result.HasMoreItems).IsFalse();
    }

    [Test]
    public async Task GetPublicListedAsync_ReturnsDtoWithCorrectFields()
    {
        var community = await _builder.CreateCommunityAsync("DTO Test Community", "dto-test");

        var result = await _repository.GetPublicListedAsync(0, 10);

        var items = result.Items.ToList();
        await Assert.That(items.Count).IsEqualTo(1);
        await Assert.That(items[0].PublicId).IsEqualTo(community.PublicId);
        await Assert.That(items[0].Name).IsEqualTo("DTO Test Community");
        await Assert.That(items[0].Slug).IsEqualTo("dto-test");
        await Assert.That(items[0].VisibilityId).IsEqualTo((int)CommunityVisibilityEnum.PublicListed);
    }

    #endregion

    #region GetForPlatformFeedAsync Tests

    [Test]
    public async Task GetForPlatformFeedAsync_MixedExposure_ReturnsOnlyExposed()
    {
        // Create communities exposed to platform feed
        var exposed1 = await _builder.CreateCommunityAsync("Alpha Exposed");
        exposed1.ExposeToPlatformFeed = true;
        await _db.Context.SaveChangesAsync();

        var exposed2 = await _builder.CreateCommunityAsync("Beta Exposed");
        exposed2.ExposeToPlatformFeed = true;
        await _db.Context.SaveChangesAsync();

        // Create community NOT exposed (default is false)
        await _builder.CreateCommunityAsync("Not Exposed");

        var result = await _repository.GetForPlatformFeedAsync(0, 10);

        await Assert.That(result.Items.Count()).IsEqualTo(2);
        await Assert.That(result.HasMoreItems).IsFalse();

        var items = result.Items.ToList();
        // Ordered by Name
        await Assert.That(items[0].Name).IsEqualTo("Alpha Exposed");
        await Assert.That(items[1].Name).IsEqualTo("Beta Exposed");
    }

    [Test]
    public async Task GetForPlatformFeedAsync_Pagination_HasMoreItemsCorrect()
    {
        for (var i = 0; i < 3; i++)
        {
            var community = await _builder.CreateCommunityAsync($"Feed Community {i:D2}");
            community.ExposeToPlatformFeed = true;
            await _db.Context.SaveChangesAsync();
        }

        var result = await _repository.GetForPlatformFeedAsync(0, 2);

        await Assert.That(result.Items.Count()).IsEqualTo(2);
        await Assert.That(result.HasMoreItems).IsTrue();
    }

    [Test]
    public async Task GetForPlatformFeedAsync_NoneExposed_ReturnsEmpty()
    {
        // Create communities with default ExposeToPlatformFeed=false
        await _builder.CreateCommunityAsync("Hidden Community 1");
        await _builder.CreateCommunityAsync("Hidden Community 2");

        var result = await _repository.GetForPlatformFeedAsync(0, 10);

        await Assert.That(result.Items.Count()).IsEqualTo(0);
        await Assert.That(result.HasMoreItems).IsFalse();
    }

    #endregion
}
