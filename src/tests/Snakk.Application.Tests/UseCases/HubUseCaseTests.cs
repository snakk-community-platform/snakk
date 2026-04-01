using NSubstitute;
using Snakk.Application.UseCases;
using Snakk.Domain.Entities;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Models;

namespace Snakk.Application.Tests.UseCases;

public class HubUseCaseTests
{
    private readonly IHubRepository _hubRepository = Substitute.For<IHubRepository>();
    private readonly ICommunityRepository _communityRepository = Substitute.For<ICommunityRepository>();
    private HubUseCase _useCase = null!;

    [Before(Test)]
    public void Setup()
    {
        _useCase = new HubUseCase(_hubRepository, _communityRepository);
    }

    #region CreateHubAsync Tests

    [Test]
    public async Task CreateHubAsync_WithValidParameters_CreatesHub()
    {
        var communityId = CommunityId.New();
        const string name = "Test Hub";
        const string slug = "test-hub";
        const string description = "A test hub";
        var community = Community.Create("Test Community", "test-community");

        _communityRepository.GetByPublicIdAsync(communityId).Returns(community);

        var result = await _useCase.CreateHubAsync(communityId, name, slug, description);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsNotNull();
        await Assert.That(result.Value!.Name).IsEqualTo(name);
        await Assert.That(result.Value.Slug).IsEqualTo(slug);
        await Assert.That(result.Value.Description).IsEqualTo(description);
        await Assert.That(result.Value.CommunityId).IsEqualTo(communityId);
        await _hubRepository.Received(1).AddAsync(Arg.Any<Hub>());
    }

    [Test]
    public async Task CreateHubAsync_WithNonExistentCommunity_ReturnsFailure()
    {
        var communityId = CommunityId.New();
        _communityRepository.GetByPublicIdAsync(communityId).Returns((Community?)null);

        var result = await _useCase.CreateHubAsync(communityId, "Hub", "hub");

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Community");
        await Assert.That(result.Error).Contains("not found");
        await _hubRepository.DidNotReceive().AddAsync(Arg.Any<Hub>());
    }

    [Test]
    public async Task CreateHubAsync_WithNullDescription_Succeeds()
    {
        var communityId = CommunityId.New();
        var community = Community.Create("Test Community", "test-community");
        _communityRepository.GetByPublicIdAsync(communityId).Returns(community);

        var result = await _useCase.CreateHubAsync(communityId, "Hub", "hub", description: null);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Description).IsNull();
    }

    #endregion

    #region GetHubAsync Tests

    [Test]
    public async Task GetHubAsync_WithExistingHub_ReturnsHub()
    {
        var hub = Hub.Create(CommunityId.New(), "Test Hub", "test-hub");
        var hubId = hub.PublicId;
        _hubRepository.GetByPublicIdAsync(hubId).Returns(hub);

        var result = await _useCase.GetHubAsync(hubId);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsEqualTo(hub);
    }

    [Test]
    public async Task GetHubAsync_WithNonExistentHub_ReturnsFailure()
    {
        var hubId = HubId.New();
        _hubRepository.GetByPublicIdAsync(hubId).Returns((Hub?)null);

        var result = await _useCase.GetHubAsync(hubId);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("not found");
    }

    #endregion

    #region GetHubBySlugAsync Tests

    [Test]
    public async Task GetHubBySlugAsync_WithExistingSlug_ReturnsHub()
    {
        const string slug = "test-hub";
        const string communitySlug = "main";
        var hub = Hub.Create(CommunityId.New(), "Test Hub", slug);
        _hubRepository.GetBySlugAsync(slug, communitySlug).Returns(hub);

        var result = await _useCase.GetHubBySlugAsync(slug, communitySlug);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsEqualTo(hub);
    }

    [Test]
    public async Task GetHubBySlugAsync_WithNonExistentSlug_ReturnsFailure()
    {
        const string slug = "non-existent";
        const string communitySlug = "main";
        _hubRepository.GetBySlugAsync(slug, communitySlug).Returns((Hub?)null);

        var result = await _useCase.GetHubBySlugAsync(slug, communitySlug);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("not found");
    }

    #endregion

    #region GetAllHubsAsync Tests

    [Test]
    public async Task GetAllHubsAsync_ReturnsPagedResults()
    {
        var hubs = new List<Hub> { Hub.Create(CommunityId.New(), "Hub 1", "hub-1"), Hub.Create(CommunityId.New(), "Hub 2", "hub-2") };
        var pagedResult = new PagedResult<Hub> { Items = hubs, Offset = 0, PageSize = 20, HasMoreItems = false };
        _hubRepository.GetFilteredForDisplayAsync(0, 20).Returns(pagedResult);

        var result = await _useCase.GetAllHubsAsync(0, 20);

        await Assert.That(result).IsNotNull();
        await Assert.That(result.Items).Count().IsEqualTo(2);
        await Assert.That(result.HasMoreItems).IsFalse();
    }

    #endregion

    #region GetHubsByCommunityAsync Tests

    [Test]
    public async Task GetHubsByCommunityAsync_ReturnsPagedResults()
    {
        var communityId = CommunityId.New();
        var hubs = new List<Hub> { Hub.Create(communityId, "Hub 1", "hub-1"), Hub.Create(communityId, "Hub 2", "hub-2") };
        var pagedResult = new PagedResult<Hub> { Items = hubs, Offset = 0, PageSize = 20, HasMoreItems = false };
        _hubRepository.GetByCommunityAsync(communityId, 0, 20, null).Returns(pagedResult);

        var result = await _useCase.GetHubsByCommunityAsync(communityId, 0, 20);

        await Assert.That(result).IsNotNull();
        await Assert.That(result.Items).Count().IsEqualTo(2);
    }

    [Test]
    public async Task GetHubsByCommunityAsync_WithCustomPagination_PassesParameters()
    {
        var communityId = CommunityId.New();
        var pagedResult = new PagedResult<Hub> { Items = [], Offset = 5, PageSize = 10, HasMoreItems = true };
        _hubRepository.GetByCommunityAsync(communityId, 5, 10, null).Returns(pagedResult);

        var result = await _useCase.GetHubsByCommunityAsync(communityId, 5, 10);

        await Assert.That(result.Offset).IsEqualTo(5);
        await Assert.That(result.PageSize).IsEqualTo(10);
        await Assert.That(result.HasMoreItems).IsTrue();
    }

    #endregion

    #region UpdateHubNameAsync Tests

    [Test]
    public async Task UpdateHubNameAsync_WithExistingHub_UpdatesName()
    {
        var hub = Hub.Create(CommunityId.New(), "Old Name", "test-hub");
        var hubId = hub.PublicId;
        const string newName = "New Name";
        _hubRepository.GetByPublicIdAsync(hubId).Returns(hub);

        var result = await _useCase.UpdateHubNameAsync(hubId, newName);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Name).IsEqualTo(newName);
        await _hubRepository.Received(1).UpdateAsync(hub);
    }

    [Test]
    public async Task UpdateHubNameAsync_WithNonExistentHub_ReturnsFailure()
    {
        var hubId = HubId.New();
        _hubRepository.GetByPublicIdAsync(hubId).Returns((Hub?)null);

        var result = await _useCase.UpdateHubNameAsync(hubId, "New Name");

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("not found");
        await _hubRepository.DidNotReceive().UpdateAsync(Arg.Any<Hub>());
    }

    #endregion

    #region UpdateHubDescriptionAsync Tests

    [Test]
    public async Task UpdateHubDescriptionAsync_WithExistingHub_UpdatesDescription()
    {
        var hub = Hub.Create(CommunityId.New(), "Test Hub", "test-hub", "Old Description");
        var hubId = hub.PublicId;
        const string newDescription = "New Description";
        _hubRepository.GetByPublicIdAsync(hubId).Returns(hub);

        var result = await _useCase.UpdateHubDescriptionAsync(hubId, newDescription);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Description).IsEqualTo(newDescription);
        await _hubRepository.Received(1).UpdateAsync(hub);
    }

    [Test]
    public async Task UpdateHubDescriptionAsync_WithNullDescription_ClearsDescription()
    {
        var hub = Hub.Create(CommunityId.New(), "Test Hub", "test-hub", "Some Description");
        var hubId = hub.PublicId;
        _hubRepository.GetByPublicIdAsync(hubId).Returns(hub);

        var result = await _useCase.UpdateHubDescriptionAsync(hubId, null);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Description).IsNull();
    }

    [Test]
    public async Task UpdateHubDescriptionAsync_WithNonExistentHub_ReturnsFailure()
    {
        var hubId = HubId.New();
        _hubRepository.GetByPublicIdAsync(hubId).Returns((Hub?)null);

        var result = await _useCase.UpdateHubDescriptionAsync(hubId, "New Description");

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("not found");
    }

    #endregion

    #region UpdateHubSlugAsync Tests

    [Test]
    public async Task UpdateHubSlugAsync_WithExistingHub_UpdatesSlug()
    {
        var hub = Hub.Create(CommunityId.New(), "Test Hub", "old-slug");
        var hubId = hub.PublicId;
        const string newSlug = "new-slug";
        _hubRepository.GetByPublicIdAsync(hubId).Returns(hub);

        var result = await _useCase.UpdateHubSlugAsync(hubId, newSlug);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Slug).IsEqualTo(newSlug);
        await _hubRepository.Received(1).UpdateAsync(hub);
    }

    [Test]
    public async Task UpdateHubSlugAsync_WithNonExistentHub_ReturnsFailure()
    {
        var hubId = HubId.New();
        _hubRepository.GetByPublicIdAsync(hubId).Returns((Hub?)null);

        var result = await _useCase.UpdateHubSlugAsync(hubId, "new-slug");

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("not found");
        await _hubRepository.DidNotReceive().UpdateAsync(Arg.Any<Hub>());
    }

    #endregion
}
