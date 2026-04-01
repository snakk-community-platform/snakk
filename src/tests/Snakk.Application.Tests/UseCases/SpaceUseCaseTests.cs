using NSubstitute;
using Snakk.Application.UseCases;
using Snakk.Domain.Entities;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Models;

namespace Snakk.Application.Tests.UseCases;

public class SpaceUseCaseTests
{
    private readonly ISpaceRepository _spaceRepository = Substitute.For<ISpaceRepository>();
    private readonly IHubRepository _hubRepository = Substitute.For<IHubRepository>();
    private SpaceUseCase _useCase = null!;

    [Before(Test)]
    public void Setup()
    {
        _useCase = new SpaceUseCase(_spaceRepository, _hubRepository);
    }

    #region CreateSpaceAsync Tests

    [Test]
    public async Task CreateSpaceAsync_WithValidParameters_CreatesSpace()
    {
        var hubId = HubId.New();
        const string name = "Test Space";
        const string slug = "test-space";
        const string description = "A test space";
        var hub = Hub.Create(CommunityId.New(), "Test Hub", "test-hub");

        _hubRepository.GetByPublicIdAsync(hubId).Returns(hub);

        var result = await _useCase.CreateSpaceAsync(hubId, name, slug, description);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsNotNull();
        await Assert.That(result.Value!.Name).IsEqualTo(name);
        await Assert.That(result.Value.Slug).IsEqualTo(slug);
        await Assert.That(result.Value.Description).IsEqualTo(description);
        await Assert.That(result.Value.HubId).IsEqualTo(hubId);
        await _spaceRepository.Received(1).AddAsync(Arg.Any<Space>());
    }

    [Test]
    public async Task CreateSpaceAsync_WithNonExistentHub_ReturnsFailure()
    {
        var hubId = HubId.New();
        _hubRepository.GetByPublicIdAsync(hubId).Returns((Hub?)null);

        var result = await _useCase.CreateSpaceAsync(hubId, "Space", "space");

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Hub");
        await Assert.That(result.Error).Contains("not found");
        await _spaceRepository.DidNotReceive().AddAsync(Arg.Any<Space>());
    }

    [Test]
    public async Task CreateSpaceAsync_WithNullDescription_Succeeds()
    {
        var hubId = HubId.New();
        var hub = Hub.Create(CommunityId.New(), "Test Hub", "test-hub");
        _hubRepository.GetByPublicIdAsync(hubId).Returns(hub);

        var result = await _useCase.CreateSpaceAsync(hubId, "Space", "space", description: null);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Description).IsNull();
    }

    #endregion

    #region GetSpaceAsync Tests

    [Test]
    public async Task GetSpaceAsync_WithExistingSpace_ReturnsSpace()
    {
        var space = Space.Create(HubId.New(), "Test Space", "test-space");
        var spaceId = space.PublicId;
        _spaceRepository.GetByPublicIdAsync(spaceId).Returns(space);

        var result = await _useCase.GetSpaceAsync(spaceId);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsEqualTo(space);
    }

    [Test]
    public async Task GetSpaceAsync_WithNonExistentSpace_ReturnsFailure()
    {
        var spaceId = SpaceId.New();
        _spaceRepository.GetByPublicIdAsync(spaceId).Returns((Space?)null);

        var result = await _useCase.GetSpaceAsync(spaceId);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("not found");
    }

    #endregion

    #region GetSpaceBySlugAsync Tests

    [Test]
    public async Task GetSpaceBySlugAsync_WithExistingSlug_ReturnsSpace()
    {
        const string slug = "test-space";
        const string hubSlug = "test-hub";
        var space = Space.Create(HubId.New(), "Test Space", slug);
        _spaceRepository.GetBySlugAsync(slug, hubSlug).Returns(space);

        var result = await _useCase.GetSpaceBySlugAsync(slug, hubSlug);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsEqualTo(space);
    }

    [Test]
    public async Task GetSpaceBySlugAsync_WithNonExistentSlug_ReturnsFailure()
    {
        const string slug = "non-existent";
        const string hubSlug = "test-hub";
        _spaceRepository.GetBySlugAsync(slug, hubSlug).Returns((Space?)null);

        var result = await _useCase.GetSpaceBySlugAsync(slug, hubSlug);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("not found");
    }

    #endregion

    #region GetSpacesByHubAsync Tests

    [Test]
    public async Task GetSpacesByHubAsync_ReturnsPagedResults()
    {
        var hubId = HubId.New();
        var spaces = new List<Space> { Space.Create(hubId, "Space 1", "space-1"), Space.Create(hubId, "Space 2", "space-2") };
        var pagedResult = new PagedResult<Space> { Items = spaces, Offset = 0, PageSize = 20, HasMoreItems = false };
        _spaceRepository.GetFilteredForDisplayAsync(hubId, 0, 20).Returns(pagedResult);

        var result = await _useCase.GetSpacesByHubAsync(hubId, 0, 20);

        await Assert.That(result).IsNotNull();
        await Assert.That(result.Items).Count().IsEqualTo(2);
        await Assert.That(result.HasMoreItems).IsFalse();
    }

    [Test]
    public async Task GetSpacesByHubAsync_WithCustomPagination_PassesParameters()
    {
        var hubId = HubId.New();
        var pagedResult = new PagedResult<Space> { Items = [], Offset = 10, PageSize = 5, HasMoreItems = true };
        _spaceRepository.GetFilteredForDisplayAsync(hubId, 10, 5).Returns(pagedResult);

        var result = await _useCase.GetSpacesByHubAsync(hubId, 10, 5);

        await Assert.That(result.Offset).IsEqualTo(10);
        await Assert.That(result.PageSize).IsEqualTo(5);
        await Assert.That(result.HasMoreItems).IsTrue();
    }

    #endregion

    #region UpdateSpaceNameAsync Tests

    [Test]
    public async Task UpdateSpaceNameAsync_WithExistingSpace_UpdatesName()
    {
        var space = Space.Create(HubId.New(), "Old Name", "test-space");
        var spaceId = space.PublicId;
        const string newName = "New Name";
        _spaceRepository.GetByPublicIdAsync(spaceId).Returns(space);

        var result = await _useCase.UpdateSpaceNameAsync(spaceId, newName);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Name).IsEqualTo(newName);
        await _spaceRepository.Received(1).UpdateAsync(space);
    }

    [Test]
    public async Task UpdateSpaceNameAsync_WithNonExistentSpace_ReturnsFailure()
    {
        var spaceId = SpaceId.New();
        _spaceRepository.GetByPublicIdAsync(spaceId).Returns((Space?)null);

        var result = await _useCase.UpdateSpaceNameAsync(spaceId, "New Name");

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("not found");
        await _spaceRepository.DidNotReceive().UpdateAsync(Arg.Any<Space>());
    }

    #endregion

    #region UpdateSpaceDescriptionAsync Tests

    [Test]
    public async Task UpdateSpaceDescriptionAsync_WithExistingSpace_UpdatesDescription()
    {
        var space = Space.Create(HubId.New(), "Test Space", "test-space", "Old Description");
        var spaceId = space.PublicId;
        const string newDescription = "New Description";
        _spaceRepository.GetByPublicIdAsync(spaceId).Returns(space);

        var result = await _useCase.UpdateSpaceDescriptionAsync(spaceId, newDescription);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Description).IsEqualTo(newDescription);
        await _spaceRepository.Received(1).UpdateAsync(space);
    }

    [Test]
    public async Task UpdateSpaceDescriptionAsync_WithNullDescription_ClearsDescription()
    {
        var space = Space.Create(HubId.New(), "Test Space", "test-space", "Some Description");
        var spaceId = space.PublicId;
        _spaceRepository.GetByPublicIdAsync(spaceId).Returns(space);

        var result = await _useCase.UpdateSpaceDescriptionAsync(spaceId, null);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Description).IsNull();
    }

    [Test]
    public async Task UpdateSpaceDescriptionAsync_WithNonExistentSpace_ReturnsFailure()
    {
        var spaceId = SpaceId.New();
        _spaceRepository.GetByPublicIdAsync(spaceId).Returns((Space?)null);

        var result = await _useCase.UpdateSpaceDescriptionAsync(spaceId, "New Description");

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("not found");
    }

    #endregion

    #region UpdateSpaceSlugAsync Tests

    [Test]
    public async Task UpdateSpaceSlugAsync_WithExistingSpace_UpdatesSlug()
    {
        var space = Space.Create(HubId.New(), "Test Space", "old-slug");
        var spaceId = space.PublicId;
        const string newSlug = "new-slug";
        _spaceRepository.GetByPublicIdAsync(spaceId).Returns(space);

        var result = await _useCase.UpdateSpaceSlugAsync(spaceId, newSlug);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Slug).IsEqualTo(newSlug);
        await _spaceRepository.Received(1).UpdateAsync(space);
    }

    [Test]
    public async Task UpdateSpaceSlugAsync_WithNonExistentSpace_ReturnsFailure()
    {
        var spaceId = SpaceId.New();
        _spaceRepository.GetByPublicIdAsync(spaceId).Returns((Space?)null);

        var result = await _useCase.UpdateSpaceSlugAsync(spaceId, "new-slug");

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("not found");
        await _spaceRepository.DidNotReceive().UpdateAsync(Arg.Any<Space>());
    }

    #endregion
}
