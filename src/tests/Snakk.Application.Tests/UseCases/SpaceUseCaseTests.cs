using Moq;
using Snakk.Application.UseCases;
using Snakk.Domain.Entities;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Models;

namespace Snakk.Application.Tests.UseCases;

public class SpaceUseCaseTests
{
    private readonly Mock<ISpaceRepository> _mockSpaceRepository = new();
    private readonly Mock<IHubRepository> _mockHubRepository = new();
    private SpaceUseCase _useCase = null!;

    [Before(Test)]
    public void Setup()
    {
        _useCase = new SpaceUseCase(_mockSpaceRepository.Object, _mockHubRepository.Object);
    }

    #region CreateSpaceAsync Tests

    [Test]
    public async Task CreateSpaceAsync_WithValidParameters_CreatesSpace()
    {
        // Arrange
        var hubId = HubId.New();
        const string name = "Test Space";
        const string slug = "test-space";
        const string description = "A test space";

        var hub = Hub.Create(CommunityId.New(), "Test Hub", "test-hub");

        _mockHubRepository.Setup(r => r.GetByPublicIdAsync(hubId))
            .ReturnsAsync(hub);

        // Act
        var result = await _useCase.CreateSpaceAsync(hubId, name, slug, description);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsNotNull();
        await Assert.That(result.Value!.Name).IsEqualTo(name);
        await Assert.That(result.Value.Slug).IsEqualTo(slug);
        await Assert.That(result.Value.Description).IsEqualTo(description);
        await Assert.That(result.Value.HubId).IsEqualTo(hubId);

        _mockSpaceRepository.Verify(r => r.AddAsync(It.IsAny<Space>()), Times.Once);
    }

    [Test]
    public async Task CreateSpaceAsync_WithNonExistentHub_ReturnsFailure()
    {
        // Arrange
        var hubId = HubId.New();

        _mockHubRepository.Setup(r => r.GetByPublicIdAsync(hubId))
            .ReturnsAsync((Hub?)null);

        // Act
        var result = await _useCase.CreateSpaceAsync(hubId, "Space", "space");

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Hub");
        await Assert.That(result.Error).Contains("not found");

        _mockSpaceRepository.Verify(r => r.AddAsync(It.IsAny<Space>()), Times.Never);
    }

    [Test]
    public async Task CreateSpaceAsync_WithNullDescription_Succeeds()
    {
        // Arrange
        var hubId = HubId.New();
        var hub = Hub.Create(CommunityId.New(), "Test Hub", "test-hub");

        _mockHubRepository.Setup(r => r.GetByPublicIdAsync(hubId))
            .ReturnsAsync(hub);

        // Act
        var result = await _useCase.CreateSpaceAsync(hubId, "Space", "space", description: null);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Description).IsNull();
    }

    #endregion

    #region GetSpaceAsync Tests

    [Test]
    public async Task GetSpaceAsync_WithExistingSpace_ReturnsSpace()
    {
        // Arrange
        var space = Space.Create(HubId.New(), "Test Space", "test-space");
        var spaceId = space.PublicId;

        _mockSpaceRepository.Setup(r => r.GetByPublicIdAsync(spaceId))
            .ReturnsAsync(space);

        // Act
        var result = await _useCase.GetSpaceAsync(spaceId);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsEqualTo(space);
    }

    [Test]
    public async Task GetSpaceAsync_WithNonExistentSpace_ReturnsFailure()
    {
        // Arrange
        var spaceId = SpaceId.New();

        _mockSpaceRepository.Setup(r => r.GetByPublicIdAsync(spaceId))
            .ReturnsAsync((Space?)null);

        // Act
        var result = await _useCase.GetSpaceAsync(spaceId);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("not found");
    }

    #endregion

    #region GetSpaceBySlugAsync Tests

    [Test]
    public async Task GetSpaceBySlugAsync_WithExistingSlug_ReturnsSpace()
    {
        // Arrange
        const string slug = "test-space";
        var space = Space.Create(HubId.New(), "Test Space", slug);

        _mockSpaceRepository.Setup(r => r.GetBySlugAsync(slug))
            .ReturnsAsync(space);

        // Act
        var result = await _useCase.GetSpaceBySlugAsync(slug);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsEqualTo(space);
    }

    [Test]
    public async Task GetSpaceBySlugAsync_WithNonExistentSlug_ReturnsFailure()
    {
        // Arrange
        const string slug = "non-existent";

        _mockSpaceRepository.Setup(r => r.GetBySlugAsync(slug))
            .ReturnsAsync((Space?)null);

        // Act
        var result = await _useCase.GetSpaceBySlugAsync(slug);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("not found");
    }

    #endregion

    #region GetSpacesByHubAsync Tests

    [Test]
    public async Task GetSpacesByHubAsync_ReturnsPagedResults()
    {
        // Arrange
        var hubId = HubId.New();
        var spaces = new List<Space>
        {
            Space.Create(hubId, "Space 1", "space-1"),
            Space.Create(hubId, "Space 2", "space-2")
        };

        var pagedResult = new PagedResult<Space>
        {
            Items = spaces,
            Offset = 0,
            PageSize = 20,
            HasMoreItems = false
        };

        _mockSpaceRepository.Setup(r => r.GetFilteredForDisplayAsync(hubId, 0, 20))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _useCase.GetSpacesByHubAsync(hubId, 0, 20);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Items).Count().IsEqualTo(2);
        await Assert.That(result.HasMoreItems).IsFalse();
    }

    [Test]
    public async Task GetSpacesByHubAsync_WithCustomPagination_PassesParameters()
    {
        // Arrange
        var hubId = HubId.New();
        var pagedResult = new PagedResult<Space>
        {
            Items = [],
            Offset = 10,
            PageSize = 5,
            HasMoreItems = true
        };

        _mockSpaceRepository.Setup(r => r.GetFilteredForDisplayAsync(hubId, 10, 5))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _useCase.GetSpacesByHubAsync(hubId, 10, 5);

        // Assert
        await Assert.That(result.Offset).IsEqualTo(10);
        await Assert.That(result.PageSize).IsEqualTo(5);
        await Assert.That(result.HasMoreItems).IsTrue();
    }

    #endregion

    #region UpdateSpaceNameAsync Tests

    [Test]
    public async Task UpdateSpaceNameAsync_WithExistingSpace_UpdatesName()
    {
        // Arrange
        var space = Space.Create(HubId.New(), "Old Name", "test-space");
        var spaceId = space.PublicId;
        const string newName = "New Name";

        _mockSpaceRepository.Setup(r => r.GetByPublicIdAsync(spaceId))
            .ReturnsAsync(space);

        // Act
        var result = await _useCase.UpdateSpaceNameAsync(spaceId, newName);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Name).IsEqualTo(newName);

        _mockSpaceRepository.Verify(r => r.UpdateAsync(space), Times.Once);
    }

    [Test]
    public async Task UpdateSpaceNameAsync_WithNonExistentSpace_ReturnsFailure()
    {
        // Arrange
        var spaceId = SpaceId.New();

        _mockSpaceRepository.Setup(r => r.GetByPublicIdAsync(spaceId))
            .ReturnsAsync((Space?)null);

        // Act
        var result = await _useCase.UpdateSpaceNameAsync(spaceId, "New Name");

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("not found");

        _mockSpaceRepository.Verify(r => r.UpdateAsync(It.IsAny<Space>()), Times.Never);
    }

    #endregion

    #region UpdateSpaceDescriptionAsync Tests

    [Test]
    public async Task UpdateSpaceDescriptionAsync_WithExistingSpace_UpdatesDescription()
    {
        // Arrange
        var space = Space.Create(HubId.New(), "Test Space", "test-space", "Old Description");
        var spaceId = space.PublicId;
        const string newDescription = "New Description";

        _mockSpaceRepository.Setup(r => r.GetByPublicIdAsync(spaceId))
            .ReturnsAsync(space);

        // Act
        var result = await _useCase.UpdateSpaceDescriptionAsync(spaceId, newDescription);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Description).IsEqualTo(newDescription);

        _mockSpaceRepository.Verify(r => r.UpdateAsync(space), Times.Once);
    }

    [Test]
    public async Task UpdateSpaceDescriptionAsync_WithNullDescription_ClearsDescription()
    {
        // Arrange
        var space = Space.Create(HubId.New(), "Test Space", "test-space", "Some Description");
        var spaceId = space.PublicId;

        _mockSpaceRepository.Setup(r => r.GetByPublicIdAsync(spaceId))
            .ReturnsAsync(space);

        // Act
        var result = await _useCase.UpdateSpaceDescriptionAsync(spaceId, null);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Description).IsNull();
    }

    [Test]
    public async Task UpdateSpaceDescriptionAsync_WithNonExistentSpace_ReturnsFailure()
    {
        // Arrange
        var spaceId = SpaceId.New();

        _mockSpaceRepository.Setup(r => r.GetByPublicIdAsync(spaceId))
            .ReturnsAsync((Space?)null);

        // Act
        var result = await _useCase.UpdateSpaceDescriptionAsync(spaceId, "New Description");

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("not found");
    }

    #endregion

    #region UpdateSpaceSlugAsync Tests

    [Test]
    public async Task UpdateSpaceSlugAsync_WithExistingSpace_UpdatesSlug()
    {
        // Arrange
        var space = Space.Create(HubId.New(), "Test Space", "old-slug");
        var spaceId = space.PublicId;
        const string newSlug = "new-slug";

        _mockSpaceRepository.Setup(r => r.GetByPublicIdAsync(spaceId))
            .ReturnsAsync(space);

        // Act
        var result = await _useCase.UpdateSpaceSlugAsync(spaceId, newSlug);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Slug).IsEqualTo(newSlug);

        _mockSpaceRepository.Verify(r => r.UpdateAsync(space), Times.Once);
    }

    [Test]
    public async Task UpdateSpaceSlugAsync_WithNonExistentSpace_ReturnsFailure()
    {
        // Arrange
        var spaceId = SpaceId.New();

        _mockSpaceRepository.Setup(r => r.GetByPublicIdAsync(spaceId))
            .ReturnsAsync((Space?)null);

        // Act
        var result = await _useCase.UpdateSpaceSlugAsync(spaceId, "new-slug");

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("not found");

        _mockSpaceRepository.Verify(r => r.UpdateAsync(It.IsAny<Space>()), Times.Never);
    }

    #endregion
}
