using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.Database.Repositories;
using Snakk.Infrastructure.Tests.Helpers;
using Snakk.Shared.Models;

namespace Snakk.Infrastructure.Tests.Repositories;

public class SpaceRepositoryIntegrationTests : IDisposable
{
    private readonly SqliteTestDatabase _db;
    private readonly TestDataBuilder _builder;
    private readonly SpaceRepository _repository;

    public SpaceRepositoryIntegrationTests()
    {
        _db = new SqliteTestDatabase();
        _builder = new TestDataBuilder(_db.Context);
        _repository = new SpaceRepository(_db.Context);
    }

    public void Dispose() => _db.Dispose();

    #region GetByIdAsync Tests

    [Test]
    public async Task GetByIdAsync_ExistingSpace_ReturnsEntityWithHubLoaded()
    {
        var (_, community, hub, space, _, _) = await _builder.CreateFullHierarchyAsync();

        var result = await _repository.GetByIdAsync(space.Id);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Id).IsEqualTo(space.Id);
        await Assert.That(result.PublicId).IsEqualTo(space.PublicId);
        await Assert.That(result.Name).IsEqualTo(space.Name);
        await Assert.That(result.Hub).IsNotNull();
        await Assert.That(result.Hub.Id).IsEqualTo(hub.Id);
    }

    [Test]
    public async Task GetByIdAsync_NonExistentId_ReturnsNull()
    {
        var result = await _repository.GetByIdAsync(99999);

        await Assert.That(result).IsNull();
    }

    #endregion

    #region GetForUpdateAsync Tests

    [Test]
    public async Task GetForUpdateAsync_ExistingSpace_ReturnsWithHubLoaded()
    {
        var (user, _, hub, space, discussion, _) = await _builder.CreateFullHierarchyAsync();

        var result = await _repository.GetForUpdateAsync(space.PublicId);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.PublicId).IsEqualTo(space.PublicId);
        await Assert.That(result.Hub).IsNotNull();
        await Assert.That(result.Hub.Id).IsEqualTo(hub.Id);
    }

    [Test]
    public async Task GetForUpdateAsync_NonExistentPublicId_ReturnsNull()
    {
        var result = await _repository.GetForUpdateAsync("nonexistent_public_id");

        await Assert.That(result).IsNull();
    }

    #endregion

    #region GetAllAsync Tests

    [Test]
    public async Task GetAllAsync_MultipleSpaces_ReturnsAllWithHubLoaded()
    {
        var community = await _builder.CreateCommunityAsync();
        var hub = await _builder.CreateHubAsync(community.Id);
        var space1 = await _builder.CreateSpaceAsync(hub.Id, "Space Alpha");
        var space2 = await _builder.CreateSpaceAsync(hub.Id, "Space Beta");
        var space3 = await _builder.CreateSpaceAsync(hub.Id, "Space Gamma");

        var result = (await _repository.GetAllAsync()).ToList();

        await Assert.That(result.Count).IsEqualTo(3);
        await Assert.That(result.All(s => s.Hub != null)).IsTrue();
        await Assert.That(result.All(s => s.Hub.Id == hub.Id)).IsTrue();
    }

    #endregion

    #region GetForDisplayAsync Tests

    [Test]
    public async Task GetForDisplayAsync_ExistingSpace_ReturnsDtoWithCorrectFields()
    {
        var (_, community, hub, space, _, _) = await _builder.CreateFullHierarchyAsync();

        var result = await _repository.GetForDisplayAsync(space.PublicId);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.PublicId).IsEqualTo(space.PublicId);
        await Assert.That(result.Name).IsEqualTo(space.Name);
        await Assert.That(result.Slug).IsEqualTo(space.Slug);
        await Assert.That(result.CreatedAt).IsEqualTo(space.CreatedAt);
        await Assert.That(result.HubPublicId).IsEqualTo(hub.PublicId);
        await Assert.That(result.HubName).IsEqualTo(hub.Name);
    }

    [Test]
    public async Task GetForDisplayAsync_NonExistentPublicId_ReturnsNull()
    {
        var result = await _repository.GetForDisplayAsync("nonexistent_public_id");

        await Assert.That(result).IsNull();
    }

    #endregion

    #region GetByPublicIdAsync Tests

    [Test]
    public async Task GetByPublicIdAsync_ExistingSpace_ReturnsEntityWithHub()
    {
        var (_, _, hub, space, _, _) = await _builder.CreateFullHierarchyAsync();

        var result = await _repository.GetByPublicIdAsync(space.PublicId);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.PublicId).IsEqualTo(space.PublicId);
        await Assert.That(result.Hub).IsNotNull();
        await Assert.That(result.Hub.Id).IsEqualTo(hub.Id);
    }

    [Test]
    public async Task GetByPublicIdAsync_NonExistentPublicId_ReturnsNull()
    {
        var result = await _repository.GetByPublicIdAsync("nonexistent_public_id");

        await Assert.That(result).IsNull();
    }

    #endregion

    #region GetBySlugAsync Tests

    [Test]
    public async Task GetBySlugAsync_ExistingSlug_ReturnsEntityWithHub()
    {
        var (_, _, hub, space, _, _) = await _builder.CreateFullHierarchyAsync();

        var result = await _repository.GetBySlugAsync(space.Slug);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Slug).IsEqualTo(space.Slug);
        await Assert.That(result.PublicId).IsEqualTo(space.PublicId);
        await Assert.That(result.Hub).IsNotNull();
        await Assert.That(result.Hub.Id).IsEqualTo(hub.Id);
    }

    [Test]
    public async Task GetBySlugAsync_NonExistentSlug_ReturnsNull()
    {
        var result = await _repository.GetBySlugAsync("nonexistent-slug");

        await Assert.That(result).IsNull();
    }

    #endregion

    #region GetFilteredForDisplayAsync Tests

    [Test]
    public async Task GetFilteredForDisplayAsync_ReturnsOnlySpacesForSpecifiedHub()
    {
        var community = await _builder.CreateCommunityAsync();
        var hub1 = await _builder.CreateHubAsync(community.Id, "Hub One");
        var hub2 = await _builder.CreateHubAsync(community.Id, "Hub Two");
        var space1 = await _builder.CreateSpaceAsync(hub1.Id, "Space in Hub1");
        var space2 = await _builder.CreateSpaceAsync(hub2.Id, "Space in Hub2");

        var result = await _repository.GetFilteredForDisplayAsync(hub1.PublicId, 0, 10);

        await Assert.That(result.Items.Count()).IsEqualTo(1);
        await Assert.That(result.Items.First().PublicId).IsEqualTo(space1.PublicId);
        await Assert.That(result.Items.First().HubPublicId).IsEqualTo(hub1.PublicId);
        await Assert.That(result.HasMoreItems).IsFalse();
    }

    [Test]
    public async Task GetFilteredForDisplayAsync_PaginationWithHasMoreItems()
    {
        var community = await _builder.CreateCommunityAsync();
        var hub = await _builder.CreateHubAsync(community.Id);
        await _builder.CreateSpaceAsync(hub.Id, "Alpha Space");
        await _builder.CreateSpaceAsync(hub.Id, "Beta Space");
        await _builder.CreateSpaceAsync(hub.Id, "Gamma Space");

        // Request page size of 2 with 3 items available
        var result = await _repository.GetFilteredForDisplayAsync(hub.PublicId, 0, 2);

        await Assert.That(result.Items.Count()).IsEqualTo(2);
        await Assert.That(result.HasMoreItems).IsTrue();
        await Assert.That(result.Offset).IsEqualTo(0);
        await Assert.That(result.PageSize).IsEqualTo(2);
        // Ordered by name: Alpha, Beta should be first page
        await Assert.That(result.Items.First().Name).IsEqualTo("Alpha Space");
    }

    [Test]
    public async Task GetFilteredForDisplayAsync_EmptyHub_ReturnsEmptyResult()
    {
        var community = await _builder.CreateCommunityAsync();
        var hub = await _builder.CreateHubAsync(community.Id);

        var result = await _repository.GetFilteredForDisplayAsync(hub.PublicId, 0, 10);

        await Assert.That(result.Items.Count()).IsEqualTo(0);
        await Assert.That(result.HasMoreItems).IsFalse();
    }

    #endregion
}
