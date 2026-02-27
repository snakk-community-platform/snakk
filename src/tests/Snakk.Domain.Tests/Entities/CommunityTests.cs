using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;

namespace Snakk.Domain.Tests.Entities;

public class CommunityTests
{
    #region Create Tests

    [Test]
    public async Task Create_WithValidNameAndSlug_CreatesCommunity()
    {
        // Act
        var community = Community.Create("Test Community", "test-community");

        // Assert
        await Assert.That(community).IsNotNull();
        await Assert.That(community.PublicId).IsNotNull();
        await Assert.That(community.Name).IsEqualTo("Test Community");
        await Assert.That(community.Slug).IsEqualTo("test-community");
        await Assert.That(community.CreatedAt).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task Create_SetsDefaultVisibility_ToPublicListed()
    {
        // Act
        var community = Community.Create("Test", "test");

        // Assert
        await Assert.That(community.Visibility).IsEqualTo(CommunityVisibility.PublicListed);
    }

    [Test]
    public async Task Create_SetsExposeToPlatformFeed_ToTrue()
    {
        // Act
        var community = Community.Create("Test", "test");

        // Assert
        await Assert.That(community.ExposeToPlatformFeed).IsTrue();
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task Create_WithEmptyName_ThrowsArgumentException(string? emptyName)
    {
        // Act & Assert
        await Assert.That(() => Community.Create(emptyName!, "test-slug")).Throws<ArgumentException>();
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task Create_WithEmptySlug_ThrowsArgumentException(string? emptySlug)
    {
        // Act & Assert
        await Assert.That(() => Community.Create("Test Name", emptySlug!)).Throws<ArgumentException>();
    }

    [Test]
    public async Task Create_WithDescription_SetsDescription()
    {
        // Act
        var community = Community.Create("Test", "test", description: "A test community");

        // Assert
        await Assert.That(community.Description).IsEqualTo("A test community");
    }

    [Test]
    public async Task Create_WithoutDescription_DescriptionIsNull()
    {
        // Act
        var community = Community.Create("Test", "test");

        // Assert
        await Assert.That(community.Description).IsNull();
    }

    #endregion

    #region UpdateName Tests

    [Test]
    public async Task UpdateName_UpdatesNameAndSetsLastModifiedAt()
    {
        // Arrange
        var community = Community.Create("Original", "original");

        // Act
        community.UpdateName("Updated Name");

        // Assert
        await Assert.That(community.Name).IsEqualTo("Updated Name");
        await Assert.That(community.LastModifiedAt).IsNotNull();
        await Assert.That(community.LastModifiedAt!.Value).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task UpdateName_WithEmpty_ThrowsArgumentException(string? emptyName)
    {
        // Arrange
        var community = Community.Create("Original", "original");

        // Act & Assert
        await Assert.That(() => community.UpdateName(emptyName!)).Throws<ArgumentException>();
    }

    #endregion

    #region UpdateDescription Tests

    [Test]
    public async Task UpdateDescription_AllowsNull()
    {
        // Arrange
        var community = Community.Create("Test", "test", description: "Original description");

        // Act
        community.UpdateDescription(null);

        // Assert
        await Assert.That(community.Description).IsNull();
        await Assert.That(community.LastModifiedAt).IsNotNull();
    }

    [Test]
    public async Task UpdateDescription_SetsNewDescription()
    {
        // Arrange
        var community = Community.Create("Test", "test");

        // Act
        community.UpdateDescription("New description");

        // Assert
        await Assert.That(community.Description).IsEqualTo("New description");
    }

    #endregion

    #region UpdateSlug Tests

    [Test]
    public async Task UpdateSlug_UpdatesSlug()
    {
        // Arrange
        var community = Community.Create("Test", "original-slug");

        // Act
        community.UpdateSlug("new-slug");

        // Assert
        await Assert.That(community.Slug).IsEqualTo("new-slug");
        await Assert.That(community.LastModifiedAt).IsNotNull();
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task UpdateSlug_WithEmpty_ThrowsArgumentException(string? emptySlug)
    {
        // Arrange
        var community = Community.Create("Test", "original");

        // Act & Assert
        await Assert.That(() => community.UpdateSlug(emptySlug!)).Throws<ArgumentException>();
    }

    #endregion

    #region UpdateVisibility Tests

    [Test]
    public async Task UpdateVisibility_ChangesVisibility()
    {
        // Arrange
        var community = Community.Create("Test", "test");

        // Act
        community.UpdateVisibility(CommunityVisibility.PublicUnlisted);

        // Assert
        await Assert.That(community.Visibility).IsEqualTo(CommunityVisibility.PublicUnlisted);
        await Assert.That(community.LastModifiedAt).IsNotNull();
    }

    #endregion

    #region SetExposeToPlatformFeed Tests

    [Test]
    public async Task SetExposeToPlatformFeed_ChangesFlag()
    {
        // Arrange
        var community = Community.Create("Test", "test");
        await Assert.That(community.ExposeToPlatformFeed).IsTrue();

        // Act
        community.SetExposeToPlatformFeed(false);

        // Assert
        await Assert.That(community.ExposeToPlatformFeed).IsFalse();
        await Assert.That(community.LastModifiedAt).IsNotNull();
    }

    #endregion

    #region Hubs Collection Tests

    [Test]
    public async Task Hubs_InitiallyEmpty()
    {
        // Arrange & Act
        var community = Community.Create("Test", "test");

        // Assert
        await Assert.That(community.Hubs).IsEmpty();
    }

    #endregion

    #region Rehydrate Tests

    [Test]
    public async Task Rehydrate_RestoresAllProperties()
    {
        // Arrange
        var publicId = CommunityId.New();
        var createdAt = DateTime.UtcNow.AddDays(-7);
        var lastModifiedAt = DateTime.UtcNow.AddHours(-2);

        // Act
        var community = Community.Rehydrate(
            publicId,
            "Rehydrated Community",
            "rehydrated-community",
            "A rehydrated description",
            CommunityVisibility.PublicUnlisted,
            exposeToPlatformFeed: false,
            createdAt,
            lastModifiedAt);

        // Assert
        await Assert.That(community.PublicId).IsEqualTo(publicId);
        await Assert.That(community.Name).IsEqualTo("Rehydrated Community");
        await Assert.That(community.Slug).IsEqualTo("rehydrated-community");
        await Assert.That(community.Description).IsEqualTo("A rehydrated description");
        await Assert.That(community.Visibility).IsEqualTo(CommunityVisibility.PublicUnlisted);
        await Assert.That(community.ExposeToPlatformFeed).IsFalse();
        await Assert.That(community.CreatedAt).IsEqualTo(createdAt);
        await Assert.That(community.LastModifiedAt).IsEqualTo(lastModifiedAt);
    }

    #endregion
}
