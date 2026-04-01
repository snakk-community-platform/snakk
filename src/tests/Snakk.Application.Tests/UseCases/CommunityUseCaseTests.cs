using NSubstitute;
using Snakk.Application.UseCases;
using Snakk.Domain.Entities;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Models;

namespace Snakk.Application.Tests.UseCases;

public class CommunityUseCaseTests
{
    private readonly ICommunityRepository _communityRepository = Substitute.For<ICommunityRepository>();
    private CommunityUseCase _useCase = null!;

    [Before(Test)]
    public void Setup()
    {
        _useCase = new CommunityUseCase(_communityRepository);
    }

    #region CreateCommunityAsync Tests

    [Test]
    public async Task CreateCommunityAsync_WithValidParameters_CreatesCommunity()
    {
        // Arrange
        const string name = "Test Community";
        const string slug = "test-community";
        const string description = "A test community";

        _communityRepository.GetBySlugAsync(slug)
            .Returns((Community?)null);

        // Act
        var result = await _useCase.CreateCommunityAsync(name, slug, description);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsNotNull();
        await Assert.That(result.Value!.Name).IsEqualTo(name);
        await Assert.That(result.Value.Slug).IsEqualTo(slug);
        await Assert.That(result.Value.Description).IsEqualTo(description);
        await Assert.That(result.Value.Visibility).IsEqualTo(CommunityVisibility.PublicListed);
        await Assert.That(result.Value.ExposeToPlatformFeed).IsTrue();

        await _communityRepository.Received(1).AddAsync(Arg.Any<Community>());
    }

    [Test]
    public async Task CreateCommunityAsync_WithDuplicateSlug_ReturnsFailure()
    {
        // Arrange
        const string slug = "existing-slug";
        var existingCommunity = Community.Create("Existing", slug);

        _communityRepository.GetBySlugAsync(slug)
            .Returns(existingCommunity);

        // Act
        var result = await _useCase.CreateCommunityAsync("New Community", slug);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("already exists");

        await _communityRepository.DidNotReceive().AddAsync(Arg.Any<Community>());
    }

    [Test]
    public async Task CreateCommunityAsync_WithCustomVisibility_SetsVisibility()
    {
        // Arrange
        const string name = "Unlisted Community";
        const string slug = "unlisted-community";

        _communityRepository.GetBySlugAsync(slug)
            .Returns((Community?)null);

        // Act
        var result = await _useCase.CreateCommunityAsync(name, slug, visibility: CommunityVisibility.PublicUnlisted);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Visibility).IsEqualTo(CommunityVisibility.PublicUnlisted);
    }

    [Test]
    public async Task CreateCommunityAsync_WithExposeToPlatformFeedFalse_SetsExpose()
    {
        // Arrange
        const string name = "Private Community";
        const string slug = "private-community";

        _communityRepository.GetBySlugAsync(slug)
            .Returns((Community?)null);

        // Act
        var result = await _useCase.CreateCommunityAsync(name, slug, exposeToPlatformFeed: false);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.ExposeToPlatformFeed).IsFalse();
    }

    [Test]
    public async Task CreateCommunityAsync_WithNullDescription_Succeeds()
    {
        // Arrange
        _communityRepository.GetBySlugAsync("test")
            .Returns((Community?)null);

        // Act
        var result = await _useCase.CreateCommunityAsync("Test", "test", description: null);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Description).IsNull();
    }

    #endregion

    #region GetCommunityAsync Tests

    [Test]
    public async Task GetCommunityAsync_WithExistingCommunity_ReturnsCommunity()
    {
        // Arrange
        var community = Community.Create("Test Community", "test-community");
        var communityId = community.PublicId;

        _communityRepository.GetByPublicIdAsync(communityId)
            .Returns(community);

        // Act
        var result = await _useCase.GetCommunityAsync(communityId);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsEqualTo(community);
    }

    [Test]
    public async Task GetCommunityAsync_WithNonExistentCommunity_ReturnsFailure()
    {
        // Arrange
        var communityId = CommunityId.New();

        _communityRepository.GetByPublicIdAsync(communityId)
            .Returns((Community?)null);

        // Act
        var result = await _useCase.GetCommunityAsync(communityId);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("not found");
    }

    #endregion

    #region GetCommunityBySlugAsync Tests

    [Test]
    public async Task GetCommunityBySlugAsync_WithExistingSlug_ReturnsCommunity()
    {
        // Arrange
        const string slug = "test-community";
        var community = Community.Create("Test Community", slug);

        _communityRepository.GetBySlugAsync(slug)
            .Returns(community);

        // Act
        var result = await _useCase.GetCommunityBySlugAsync(slug);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsEqualTo(community);
    }

    [Test]
    public async Task GetCommunityBySlugAsync_WithNonExistentSlug_ReturnsFailure()
    {
        // Arrange
        const string slug = "non-existent";

        _communityRepository.GetBySlugAsync(slug)
            .Returns((Community?)null);

        // Act
        var result = await _useCase.GetCommunityBySlugAsync(slug);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("not found");
    }

    #endregion

    #region GetCommunityByDomainAsync Tests

    [Test]
    public async Task GetCommunityByDomainAsync_WithExistingDomain_ReturnsCommunity()
    {
        // Arrange
        const string domain = "example.com";
        var community = Community.Create("Test Community", "test-community");

        _communityRepository.GetByDomainAsync(domain)
            .Returns(community);

        // Act
        var result = await _useCase.GetCommunityByDomainAsync(domain);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsEqualTo(community);
    }

    [Test]
    public async Task GetCommunityByDomainAsync_WithNonExistentDomain_ReturnsFailure()
    {
        // Arrange
        const string domain = "nonexistent.com";

        _communityRepository.GetByDomainAsync(domain)
            .Returns((Community?)null);

        // Act
        var result = await _useCase.GetCommunityByDomainAsync(domain);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("not found");
    }

    #endregion

    #region GetPublicCommunitiesAsync Tests

    [Test]
    public async Task GetPublicCommunitiesAsync_ReturnsPagedResults()
    {
        // Arrange
        var communities = new List<Community>
        {
            Community.Create("Community 1", "community-1"),
            Community.Create("Community 2", "community-2")
        };

        var pagedResult = new PagedResult<Community>
        {
            Items = communities,
            Offset = 0,
            PageSize = 20,
            HasMoreItems = false
        };

        _communityRepository.GetPublicListedAsync(0, 20)
            .Returns(pagedResult);

        // Act
        var result = await _useCase.GetPublicCommunitiesAsync(0, 20);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Items).Count().IsEqualTo(2);
        await Assert.That(result.HasMoreItems).IsFalse();
    }

    [Test]
    public async Task GetPublicCommunitiesAsync_WithCustomPagination_PassesParameters()
    {
        // Arrange
        var pagedResult = new PagedResult<Community>
        {
            Items = [],
            Offset = 10,
            PageSize = 5,
            HasMoreItems = true
        };

        _communityRepository.GetPublicListedAsync(10, 5)
            .Returns(pagedResult);

        // Act
        var result = await _useCase.GetPublicCommunitiesAsync(10, 5);

        // Assert
        await Assert.That(result.Offset).IsEqualTo(10);
        await Assert.That(result.PageSize).IsEqualTo(5);
        await Assert.That(result.HasMoreItems).IsTrue();
    }

    #endregion

    #region GetCommunitiesForPlatformFeedAsync Tests

    [Test]
    public async Task GetCommunitiesForPlatformFeedAsync_ReturnsPagedResults()
    {
        // Arrange
        var communities = new List<Community>
        {
            Community.Create("Feed Community", "feed-community")
        };

        var pagedResult = new PagedResult<Community>
        {
            Items = communities,
            Offset = 0,
            PageSize = 20,
            HasMoreItems = false
        };

        _communityRepository.GetForPlatformFeedAsync(0, 20)
            .Returns(pagedResult);

        // Act
        var result = await _useCase.GetCommunitiesForPlatformFeedAsync(0, 20);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Items).Count().IsEqualTo(1);
    }

    #endregion

    #region UpdateCommunityNameAsync Tests

    [Test]
    public async Task UpdateCommunityNameAsync_WithExistingCommunity_UpdatesName()
    {
        // Arrange
        var community = Community.Create("Old Name", "test-community");
        var communityId = community.PublicId;
        const string newName = "New Name";

        _communityRepository.GetByPublicIdAsync(communityId)
            .Returns(community);

        // Act
        var result = await _useCase.UpdateCommunityNameAsync(communityId, newName);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Name).IsEqualTo(newName);

        await _communityRepository.Received(1).UpdateAsync(community);
    }

    [Test]
    public async Task UpdateCommunityNameAsync_WithNonExistentCommunity_ReturnsFailure()
    {
        // Arrange
        var communityId = CommunityId.New();

        _communityRepository.GetByPublicIdAsync(communityId)
            .Returns((Community?)null);

        // Act
        var result = await _useCase.UpdateCommunityNameAsync(communityId, "New Name");

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("not found");

        await _communityRepository.DidNotReceive().UpdateAsync(Arg.Any<Community>());
    }

    #endregion

    #region UpdateCommunityDescriptionAsync Tests

    [Test]
    public async Task UpdateCommunityDescriptionAsync_WithExistingCommunity_UpdatesDescription()
    {
        // Arrange
        var community = Community.Create("Test", "test", "Old Description");
        var communityId = community.PublicId;
        const string newDescription = "New Description";

        _communityRepository.GetByPublicIdAsync(communityId)
            .Returns(community);

        // Act
        var result = await _useCase.UpdateCommunityDescriptionAsync(communityId, newDescription);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Description).IsEqualTo(newDescription);

        await _communityRepository.Received(1).UpdateAsync(community);
    }

    [Test]
    public async Task UpdateCommunityDescriptionAsync_WithNullDescription_ClearsDescription()
    {
        // Arrange
        var community = Community.Create("Test", "test", "Some Description");
        var communityId = community.PublicId;

        _communityRepository.GetByPublicIdAsync(communityId)
            .Returns(community);

        // Act
        var result = await _useCase.UpdateCommunityDescriptionAsync(communityId, null);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Description).IsNull();
    }

    [Test]
    public async Task UpdateCommunityDescriptionAsync_WithNonExistentCommunity_ReturnsFailure()
    {
        // Arrange
        var communityId = CommunityId.New();

        _communityRepository.GetByPublicIdAsync(communityId)
            .Returns((Community?)null);

        // Act
        var result = await _useCase.UpdateCommunityDescriptionAsync(communityId, "New Description");

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("not found");
    }

    #endregion

    #region UpdateCommunityVisibilityAsync Tests

    [Test]
    public async Task UpdateCommunityVisibilityAsync_WithExistingCommunity_UpdatesVisibility()
    {
        // Arrange
        var community = Community.Create("Test", "test");
        var communityId = community.PublicId;

        _communityRepository.GetByPublicIdAsync(communityId)
            .Returns(community);

        // Act
        var result = await _useCase.UpdateCommunityVisibilityAsync(communityId, CommunityVisibility.PublicUnlisted);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Visibility).IsEqualTo(CommunityVisibility.PublicUnlisted);

        await _communityRepository.Received(1).UpdateAsync(community);
    }

    [Test]
    public async Task UpdateCommunityVisibilityAsync_WithNonExistentCommunity_ReturnsFailure()
    {
        // Arrange
        var communityId = CommunityId.New();

        _communityRepository.GetByPublicIdAsync(communityId)
            .Returns((Community?)null);

        // Act
        var result = await _useCase.UpdateCommunityVisibilityAsync(communityId, CommunityVisibility.PublicUnlisted);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("not found");
    }

    #endregion

    #region SetExposeToPlatformFeedAsync Tests

    [Test]
    public async Task SetExposeToPlatformFeedAsync_WithExistingCommunity_UpdatesExpose()
    {
        // Arrange
        var community = Community.Create("Test", "test");
        var communityId = community.PublicId;

        _communityRepository.GetByPublicIdAsync(communityId)
            .Returns(community);

        // Act
        var result = await _useCase.SetExposeToPlatformFeedAsync(communityId, false);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.ExposeToPlatformFeed).IsFalse();

        await _communityRepository.Received(1).UpdateAsync(community);
    }

    [Test]
    public async Task SetExposeToPlatformFeedAsync_WithNonExistentCommunity_ReturnsFailure()
    {
        // Arrange
        var communityId = CommunityId.New();

        _communityRepository.GetByPublicIdAsync(communityId)
            .Returns((Community?)null);

        // Act
        var result = await _useCase.SetExposeToPlatformFeedAsync(communityId, true);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("not found");
    }

    #endregion
}
