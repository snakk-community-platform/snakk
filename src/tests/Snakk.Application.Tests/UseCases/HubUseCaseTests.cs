using Moq;
using Snakk.Application.UseCases;
using Snakk.Domain.Entities;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Models;

namespace Snakk.Application.Tests.UseCases;

public class HubUseCaseTests
{
    private readonly Mock<IHubRepository> _mockHubRepository = new();
    private readonly Mock<ICommunityRepository> _mockCommunityRepository = new();
    private HubUseCase _useCase = null!;

    [Before(Test)]
    public void Setup()
    {
        _useCase = new HubUseCase(_mockHubRepository.Object, _mockCommunityRepository.Object);
    }

    #region CreateHubAsync Tests

    [Test]
    public async Task CreateHubAsync_WithValidParameters_CreatesHub()
    {
        // Arrange
        var communityId = CommunityId.New();
        const string name = "Test Hub";
        const string slug = "test-hub";
        const string description = "A test hub";

        var community = Community.Create("Test Community", "test-community");

        _mockCommunityRepository.Setup(r => r.GetByPublicIdAsync(communityId))
            .ReturnsAsync(community);

        // Act
        var result = await _useCase.CreateHubAsync(communityId, name, slug, description);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsNotNull();
        await Assert.That(result.Value!.Name).IsEqualTo(name);
        await Assert.That(result.Value.Slug).IsEqualTo(slug);
        await Assert.That(result.Value.Description).IsEqualTo(description);
        await Assert.That(result.Value.CommunityId).IsEqualTo(communityId);

        _mockHubRepository.Verify(r => r.AddAsync(It.IsAny<Hub>()), Times.Once);
    }

    [Test]
    public async Task CreateHubAsync_WithNonExistentCommunity_ReturnsFailure()
    {
        // Arrange
        var communityId = CommunityId.New();

        _mockCommunityRepository.Setup(r => r.GetByPublicIdAsync(communityId))
            .ReturnsAsync((Community?)null);

        // Act
        var result = await _useCase.CreateHubAsync(communityId, "Hub", "hub");

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Community");
        await Assert.That(result.Error).Contains("not found");

        _mockHubRepository.Verify(r => r.AddAsync(It.IsAny<Hub>()), Times.Never);
    }

    [Test]
    public async Task CreateHubAsync_WithNullDescription_Succeeds()
    {
        // Arrange
        var communityId = CommunityId.New();
        var community = Community.Create("Test Community", "test-community");

        _mockCommunityRepository.Setup(r => r.GetByPublicIdAsync(communityId))
            .ReturnsAsync(community);

        // Act
        var result = await _useCase.CreateHubAsync(communityId, "Hub", "hub", description: null);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Description).IsNull();
    }

    #endregion

    #region GetHubAsync Tests

    [Test]
    public async Task GetHubAsync_WithExistingHub_ReturnsHub()
    {
        // Arrange
        var hub = Hub.Create(CommunityId.New(), "Test Hub", "test-hub");
        var hubId = hub.PublicId;

        _mockHubRepository.Setup(r => r.GetByPublicIdAsync(hubId))
            .ReturnsAsync(hub);

        // Act
        var result = await _useCase.GetHubAsync(hubId);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsEqualTo(hub);
    }

    [Test]
    public async Task GetHubAsync_WithNonExistentHub_ReturnsFailure()
    {
        // Arrange
        var hubId = HubId.New();

        _mockHubRepository.Setup(r => r.GetByPublicIdAsync(hubId))
            .ReturnsAsync((Hub?)null);

        // Act
        var result = await _useCase.GetHubAsync(hubId);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("not found");
    }

    #endregion

    #region GetHubBySlugAsync Tests

    [Test]
    public async Task GetHubBySlugAsync_WithExistingSlug_ReturnsHub()
    {
        // Arrange
        const string slug = "test-hub";
        const string communitySlug = "main";
        var hub = Hub.Create(CommunityId.New(), "Test Hub", slug);

        _mockHubRepository.Setup(r => r.GetBySlugAsync(slug, communitySlug))
            .ReturnsAsync(hub);

        // Act
        var result = await _useCase.GetHubBySlugAsync(slug, communitySlug);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsEqualTo(hub);
    }

    [Test]
    public async Task GetHubBySlugAsync_WithNonExistentSlug_ReturnsFailure()
    {
        // Arrange
        const string slug = "non-existent";
        const string communitySlug = "main";

        _mockHubRepository.Setup(r => r.GetBySlugAsync(slug, communitySlug))
            .ReturnsAsync((Hub?)null);

        // Act
        var result = await _useCase.GetHubBySlugAsync(slug, communitySlug);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("not found");
    }

    #endregion

    #region GetAllHubsAsync Tests

    [Test]
    public async Task GetAllHubsAsync_ReturnsPagedResults()
    {
        // Arrange
        var hubs = new List<Hub>
        {
            Hub.Create(CommunityId.New(), "Hub 1", "hub-1"),
            Hub.Create(CommunityId.New(), "Hub 2", "hub-2")
        };

        var pagedResult = new PagedResult<Hub>
        {
            Items = hubs,
            Offset = 0,
            PageSize = 20,
            HasMoreItems = false
        };

        _mockHubRepository.Setup(r => r.GetFilteredForDisplayAsync(0, 20))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _useCase.GetAllHubsAsync(0, 20);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Items).Count().IsEqualTo(2);
        await Assert.That(result.HasMoreItems).IsFalse();
    }

    #endregion

    #region GetHubsByCommunityAsync Tests

    [Test]
    public async Task GetHubsByCommunityAsync_ReturnsPagedResults()
    {
        // Arrange
        var communityId = CommunityId.New();
        var hubs = new List<Hub>
        {
            Hub.Create(communityId, "Hub 1", "hub-1"),
            Hub.Create(communityId, "Hub 2", "hub-2")
        };

        var pagedResult = new PagedResult<Hub>
        {
            Items = hubs,
            Offset = 0,
            PageSize = 20,
            HasMoreItems = false
        };

        _mockHubRepository.Setup(r => r.GetByCommunityAsync(communityId, 0, 20))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _useCase.GetHubsByCommunityAsync(communityId, 0, 20);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Items).Count().IsEqualTo(2);
    }

    [Test]
    public async Task GetHubsByCommunityAsync_WithCustomPagination_PassesParameters()
    {
        // Arrange
        var communityId = CommunityId.New();
        var pagedResult = new PagedResult<Hub>
        {
            Items = [],
            Offset = 5,
            PageSize = 10,
            HasMoreItems = true
        };

        _mockHubRepository.Setup(r => r.GetByCommunityAsync(communityId, 5, 10))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _useCase.GetHubsByCommunityAsync(communityId, 5, 10);

        // Assert
        await Assert.That(result.Offset).IsEqualTo(5);
        await Assert.That(result.PageSize).IsEqualTo(10);
        await Assert.That(result.HasMoreItems).IsTrue();
    }

    #endregion

    #region UpdateHubNameAsync Tests

    [Test]
    public async Task UpdateHubNameAsync_WithExistingHub_UpdatesName()
    {
        // Arrange
        var hub = Hub.Create(CommunityId.New(), "Old Name", "test-hub");
        var hubId = hub.PublicId;
        const string newName = "New Name";

        _mockHubRepository.Setup(r => r.GetByPublicIdAsync(hubId))
            .ReturnsAsync(hub);

        // Act
        var result = await _useCase.UpdateHubNameAsync(hubId, newName);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Name).IsEqualTo(newName);

        _mockHubRepository.Verify(r => r.UpdateAsync(hub), Times.Once);
    }

    [Test]
    public async Task UpdateHubNameAsync_WithNonExistentHub_ReturnsFailure()
    {
        // Arrange
        var hubId = HubId.New();

        _mockHubRepository.Setup(r => r.GetByPublicIdAsync(hubId))
            .ReturnsAsync((Hub?)null);

        // Act
        var result = await _useCase.UpdateHubNameAsync(hubId, "New Name");

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("not found");

        _mockHubRepository.Verify(r => r.UpdateAsync(It.IsAny<Hub>()), Times.Never);
    }

    #endregion

    #region UpdateHubDescriptionAsync Tests

    [Test]
    public async Task UpdateHubDescriptionAsync_WithExistingHub_UpdatesDescription()
    {
        // Arrange
        var hub = Hub.Create(CommunityId.New(), "Test Hub", "test-hub", "Old Description");
        var hubId = hub.PublicId;
        const string newDescription = "New Description";

        _mockHubRepository.Setup(r => r.GetByPublicIdAsync(hubId))
            .ReturnsAsync(hub);

        // Act
        var result = await _useCase.UpdateHubDescriptionAsync(hubId, newDescription);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Description).IsEqualTo(newDescription);

        _mockHubRepository.Verify(r => r.UpdateAsync(hub), Times.Once);
    }

    [Test]
    public async Task UpdateHubDescriptionAsync_WithNullDescription_ClearsDescription()
    {
        // Arrange
        var hub = Hub.Create(CommunityId.New(), "Test Hub", "test-hub", "Some Description");
        var hubId = hub.PublicId;

        _mockHubRepository.Setup(r => r.GetByPublicIdAsync(hubId))
            .ReturnsAsync(hub);

        // Act
        var result = await _useCase.UpdateHubDescriptionAsync(hubId, null);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Description).IsNull();
    }

    [Test]
    public async Task UpdateHubDescriptionAsync_WithNonExistentHub_ReturnsFailure()
    {
        // Arrange
        var hubId = HubId.New();

        _mockHubRepository.Setup(r => r.GetByPublicIdAsync(hubId))
            .ReturnsAsync((Hub?)null);

        // Act
        var result = await _useCase.UpdateHubDescriptionAsync(hubId, "New Description");

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("not found");
    }

    #endregion

    #region UpdateHubSlugAsync Tests

    [Test]
    public async Task UpdateHubSlugAsync_WithExistingHub_UpdatesSlug()
    {
        // Arrange
        var hub = Hub.Create(CommunityId.New(), "Test Hub", "old-slug");
        var hubId = hub.PublicId;
        const string newSlug = "new-slug";

        _mockHubRepository.Setup(r => r.GetByPublicIdAsync(hubId))
            .ReturnsAsync(hub);

        // Act
        var result = await _useCase.UpdateHubSlugAsync(hubId, newSlug);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Slug).IsEqualTo(newSlug);

        _mockHubRepository.Verify(r => r.UpdateAsync(hub), Times.Once);
    }

    [Test]
    public async Task UpdateHubSlugAsync_WithNonExistentHub_ReturnsFailure()
    {
        // Arrange
        var hubId = HubId.New();

        _mockHubRepository.Setup(r => r.GetByPublicIdAsync(hubId))
            .ReturnsAsync((Hub?)null);

        // Act
        var result = await _useCase.UpdateHubSlugAsync(hubId, "new-slug");

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("not found");

        _mockHubRepository.Verify(r => r.UpdateAsync(It.IsAny<Hub>()), Times.Never);
    }

    #endregion
}
