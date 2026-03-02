using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.Database.Repositories;
using Snakk.Infrastructure.Tests.Helpers;

namespace Snakk.Infrastructure.Tests.Repositories;

public class HubRepositoryTests : IDisposable
{
    private readonly SqliteTestDatabase _db;
    private readonly TestDataBuilder _builder;
    private readonly HubRepository _repository;

    public HubRepositoryTests()
    {
        _db = new SqliteTestDatabase();
        _builder = new TestDataBuilder(_db.Context);
        _repository = new HubRepository(_db.Context);
    }

    public void Dispose() => _db.Dispose();

    #region GetByIdAsync Tests

    [Test]
    public async Task GetByIdAsync_ExistingHub_ReturnsWithCommunityLoaded()
    {
        var community = await _builder.CreateCommunityAsync("Test Community");
        var hub = await _builder.CreateHubAsync(community.Id, "Test Hub");

        var result = await _repository.GetByIdAsync(hub.Id);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Id).IsEqualTo(hub.Id);
        await Assert.That(result.Community).IsNotNull();
        await Assert.That(result.Community.Id).IsEqualTo(community.Id);
        await Assert.That(result.Community.Name).IsEqualTo("Test Community");
    }

    [Test]
    public async Task GetByIdAsync_NonExistent_ReturnsNull()
    {
        var result = await _repository.GetByIdAsync(99999);

        await Assert.That(result).IsNull();
    }

    #endregion

    #region GetForUpdateAsync Tests

    [Test]
    public async Task GetForUpdateAsync_ExistingHub_ReturnsWithCommunityLoaded()
    {
        var community = await _builder.CreateCommunityAsync();
        var hub = await _builder.CreateHubAsync(community.Id, "Hub With Spaces");

        var result = await _repository.GetForUpdateAsync(hub.PublicId);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.PublicId).IsEqualTo(hub.PublicId);
        await Assert.That(result.Community).IsNotNull();
    }

    [Test]
    public async Task GetForUpdateAsync_NonExistent_ReturnsNull()
    {
        var result = await _repository.GetForUpdateAsync("hub_nonexistent");

        await Assert.That(result).IsNull();
    }

    #endregion

    #region GetAllAsync Tests

    [Test]
    public async Task GetAllAsync_MultipleHubs_ReturnsAllWithCommunity()
    {
        var community = await _builder.CreateCommunityAsync();
        await _builder.CreateHubAsync(community.Id, "Hub One");
        await _builder.CreateHubAsync(community.Id, "Hub Two");

        var result = (await _repository.GetAllAsync()).ToList();

        await Assert.That(result.Count).IsEqualTo(2);
        await Assert.That(result[0].Community).IsNotNull();
        await Assert.That(result[1].Community).IsNotNull();
    }

    #endregion

    #region GetForDisplayAsync Tests

    [Test]
    public async Task GetForDisplayAsync_ExistingHub_ReturnsDtoWithCorrectFields()
    {
        var community = await _builder.CreateCommunityAsync("Display Community");
        var hub = await _builder.CreateHubAsync(community.Id, "Display Hub", "display-hub");

        var result = await _repository.GetForDisplayAsync(hub.PublicId);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.PublicId).IsEqualTo(hub.PublicId);
        await Assert.That(result.CommunityPublicId).IsEqualTo(community.PublicId);
        await Assert.That(result.Name).IsEqualTo("Display Hub");
        await Assert.That(result.Slug).IsEqualTo("display-hub");
    }

    [Test]
    public async Task GetForDisplayAsync_NonExistent_ReturnsNull()
    {
        var result = await _repository.GetForDisplayAsync("hub_nonexistent");

        await Assert.That(result).IsNull();
    }

    #endregion

    #region GetByPublicIdAsync Tests

    [Test]
    public async Task GetByPublicIdAsync_ExistingHub_ReturnsEntity()
    {
        var community = await _builder.CreateCommunityAsync();
        var hub = await _builder.CreateHubAsync(community.Id, "PublicId Hub");

        var result = await _repository.GetByPublicIdAsync(hub.PublicId);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.PublicId).IsEqualTo(hub.PublicId);
        await Assert.That(result.Name).IsEqualTo("PublicId Hub");
        await Assert.That(result.Community).IsNotNull();
    }

    [Test]
    public async Task GetByPublicIdAsync_NonExistent_ReturnsNull()
    {
        var result = await _repository.GetByPublicIdAsync("hub_nonexistent");

        await Assert.That(result).IsNull();
    }

    #endregion

    #region GetBySlugAsync Tests

    [Test]
    public async Task GetBySlugAsync_ExistingHub_ReturnsEntity()
    {
        var community = await _builder.CreateCommunityAsync();
        var hub = await _builder.CreateHubAsync(community.Id, "Slug Hub", "slug-hub");

        var result = await _repository.GetBySlugAsync("slug-hub");

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Slug).IsEqualTo("slug-hub");
        await Assert.That(result.Name).IsEqualTo("Slug Hub");
        await Assert.That(result.Community).IsNotNull();
    }

    [Test]
    public async Task GetBySlugAsync_NonExistent_ReturnsNull()
    {
        var result = await _repository.GetBySlugAsync("nonexistent-slug");

        await Assert.That(result).IsNull();
    }

    #endregion

    #region GetFilteredForDisplayAsync Tests

    [Test]
    public async Task GetFilteredForDisplayAsync_Pagination_HasMoreItemsCorrect()
    {
        var community = await _builder.CreateCommunityAsync();
        await _builder.CreateHubAsync(community.Id, "Alpha Hub");
        await _builder.CreateHubAsync(community.Id, "Beta Hub");
        await _builder.CreateHubAsync(community.Id, "Gamma Hub");

        var result = await _repository.GetFilteredForDisplayAsync(0, 2);

        await Assert.That(result.Items.Count()).IsEqualTo(2);
        await Assert.That(result.HasMoreItems).IsTrue();
        await Assert.That(result.PageSize).IsEqualTo(2);

        var items = result.Items.ToList();
        // Ordered by Name
        await Assert.That(items[0].Name).IsEqualTo("Alpha Hub");
        await Assert.That(items[1].Name).IsEqualTo("Beta Hub");
    }

    [Test]
    public async Task GetFilteredForDisplayAsync_Empty_ReturnsEmptyResult()
    {
        var result = await _repository.GetFilteredForDisplayAsync(0, 10);

        await Assert.That(result.Items.Count()).IsEqualTo(0);
        await Assert.That(result.HasMoreItems).IsFalse();
    }

    #endregion

    #region GetByCommunityAsync Tests

    [Test]
    public async Task GetByCommunityAsync_OnlyReturnsHubsForSpecifiedCommunity()
    {
        var community1 = await _builder.CreateCommunityAsync("Community One");
        var community2 = await _builder.CreateCommunityAsync("Community Two");

        await _builder.CreateHubAsync(community1.Id, "Hub In Comm1");
        await _builder.CreateHubAsync(community2.Id, "Hub In Comm2");

        var result = await _repository.GetByCommunityAsync(community1.Id, 0, 10);

        await Assert.That(result.Items.Count()).IsEqualTo(1);

        var items = result.Items.ToList();
        await Assert.That(items[0].Name).IsEqualTo("Hub In Comm1");
        await Assert.That(items[0].CommunityPublicId).IsEqualTo(community1.PublicId);
    }

    [Test]
    public async Task GetByCommunityAsync_Pagination_HasMoreItemsCorrect()
    {
        var community = await _builder.CreateCommunityAsync();
        await _builder.CreateHubAsync(community.Id, "Alpha Hub");
        await _builder.CreateHubAsync(community.Id, "Beta Hub");
        await _builder.CreateHubAsync(community.Id, "Gamma Hub");

        var result = await _repository.GetByCommunityAsync(community.Id, 0, 2);

        await Assert.That(result.Items.Count()).IsEqualTo(2);
        await Assert.That(result.HasMoreItems).IsTrue();

        // Second page
        var page2 = await _repository.GetByCommunityAsync(community.Id, 2, 2);

        await Assert.That(page2.Items.Count()).IsEqualTo(1);
        await Assert.That(page2.HasMoreItems).IsFalse();

        var page2Items = page2.Items.ToList();
        await Assert.That(page2Items[0].Name).IsEqualTo("Gamma Hub");
    }

    #endregion

    #region GetCommunityDbIdAsync Tests

    [Test]
    public async Task GetCommunityDbIdAsync_ExistingCommunity_ReturnsId()
    {
        var community = await _builder.CreateCommunityAsync("Lookup Community");

        var result = await _repository.GetCommunityDbIdAsync(community.PublicId);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value).IsEqualTo(community.Id);
    }

    [Test]
    public async Task GetCommunityDbIdAsync_NonExistent_ReturnsNull()
    {
        var result = await _repository.GetCommunityDbIdAsync("comm_nonexistent");

        await Assert.That(result).IsNull();
    }

    #endregion
}
