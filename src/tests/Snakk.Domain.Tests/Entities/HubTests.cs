using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;

namespace Snakk.Domain.Tests.Entities;

public class HubTests
{
    #region Create Tests

    [Test]
    public async Task Create_WithValidParams_CreatesHub()
    {
        // Arrange
        var communityId = CommunityId.New();

        // Act
        var hub = Hub.Create(communityId, "Test Hub", "test-hub");

        // Assert
        await Assert.That(hub).IsNotNull();
        await Assert.That(hub.PublicId).IsNotNull();
        await Assert.That(hub.CommunityId).IsEqualTo(communityId);
        await Assert.That(hub.Name).IsEqualTo("Test Hub");
        await Assert.That(hub.Slug).IsEqualTo("test-hub");
        await Assert.That(hub.CreatedAt).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task Create_SetsAllowAnonymousReading_ToTrue()
    {
        // Act
        var hub = Hub.Create(CommunityId.New(), "Test", "test");

        // Assert
        await Assert.That(hub.AllowAnonymousReading).IsTrue();
    }

    [Test]
    public async Task Create_SetsRequireEmailConfirmation_ToTrue()
    {
        // Act
        var hub = Hub.Create(CommunityId.New(), "Test", "test");

        // Assert
        await Assert.That(hub.RequireEmailConfirmation).IsTrue();
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task Create_WithEmptyName_ThrowsArgumentException(string? emptyName)
    {
        // Act & Assert
        await Assert.That(() => Hub.Create(CommunityId.New(), emptyName!, "test-slug")).Throws<ArgumentException>();
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task Create_WithEmptySlug_ThrowsArgumentException(string? emptySlug)
    {
        // Act & Assert
        await Assert.That(() => Hub.Create(CommunityId.New(), "Test Name", emptySlug!)).Throws<ArgumentException>();
    }

    [Test]
    public async Task Create_WithDescription_SetsDescription()
    {
        // Act
        var hub = Hub.Create(CommunityId.New(), "Test", "test", description: "A test hub");

        // Assert
        await Assert.That(hub.Description).IsEqualTo("A test hub");
    }

    [Test]
    public async Task Create_WithoutDescription_DescriptionIsNull()
    {
        // Act
        var hub = Hub.Create(CommunityId.New(), "Test", "test");

        // Assert
        await Assert.That(hub.Description).IsNull();
    }

    #endregion

    #region UpdateName Tests

    [Test]
    public async Task UpdateName_UpdatesNameAndLastModifiedAt()
    {
        // Arrange
        var hub = Hub.Create(CommunityId.New(), "Original", "original");

        // Act
        hub.UpdateName("Updated Name");

        // Assert
        await Assert.That(hub.Name).IsEqualTo("Updated Name");
        await Assert.That(hub.LastModifiedAt).IsNotNull();
        await Assert.That(hub.LastModifiedAt!.Value).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task UpdateName_WithEmpty_ThrowsArgumentException(string? emptyName)
    {
        // Arrange
        var hub = Hub.Create(CommunityId.New(), "Original", "original");

        // Act & Assert
        await Assert.That(() => hub.UpdateName(emptyName!)).Throws<ArgumentException>();
    }

    #endregion

    #region UpdateDescription Tests

    [Test]
    public async Task UpdateDescription_AllowsNull()
    {
        // Arrange
        var hub = Hub.Create(CommunityId.New(), "Test", "test", description: "Original");

        // Act
        hub.UpdateDescription(null);

        // Assert
        await Assert.That(hub.Description).IsNull();
        await Assert.That(hub.LastModifiedAt).IsNotNull();
    }

    [Test]
    public async Task UpdateDescription_SetsNewDescription()
    {
        // Arrange
        var hub = Hub.Create(CommunityId.New(), "Test", "test");

        // Act
        hub.UpdateDescription("New description");

        // Assert
        await Assert.That(hub.Description).IsEqualTo("New description");
    }

    #endregion

    #region UpdateSlug Tests

    [Test]
    public async Task UpdateSlug_UpdatesSlug()
    {
        // Arrange
        var hub = Hub.Create(CommunityId.New(), "Test", "original-slug");

        // Act
        hub.UpdateSlug("new-slug");

        // Assert
        await Assert.That(hub.Slug).IsEqualTo("new-slug");
        await Assert.That(hub.LastModifiedAt).IsNotNull();
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task UpdateSlug_WithEmpty_ThrowsArgumentException(string? emptySlug)
    {
        // Arrange
        var hub = Hub.Create(CommunityId.New(), "Test", "original");

        // Act & Assert
        await Assert.That(() => hub.UpdateSlug(emptySlug!)).Throws<ArgumentException>();
    }

    #endregion

    #region Spaces Collection Tests

    [Test]
    public async Task Spaces_InitiallyEmpty()
    {
        // Arrange & Act
        var hub = Hub.Create(CommunityId.New(), "Test", "test");

        // Assert
        await Assert.That(hub.Spaces).IsEmpty();
    }

    #endregion

    #region Rehydrate Tests

    [Test]
    public async Task Rehydrate_RestoresAllProperties()
    {
        // Arrange
        var publicId = HubId.New();
        var communityId = CommunityId.New();
        var createdAt = DateTime.UtcNow.AddDays(-5);
        var lastModifiedAt = DateTime.UtcNow.AddHours(-1);

        // Act
        var hub = Hub.Rehydrate(
            publicId,
            communityId,
            "Rehydrated Hub",
            "rehydrated-hub",
            "Hub description",
            allowAnonymousReading: false,
            requireEmailConfirmation: false,
            createdAt,
            lastModifiedAt);

        // Assert
        await Assert.That(hub.PublicId).IsEqualTo(publicId);
        await Assert.That(hub.CommunityId).IsEqualTo(communityId);
        await Assert.That(hub.Name).IsEqualTo("Rehydrated Hub");
        await Assert.That(hub.Slug).IsEqualTo("rehydrated-hub");
        await Assert.That(hub.Description).IsEqualTo("Hub description");
        await Assert.That(hub.AllowAnonymousReading).IsFalse();
        await Assert.That(hub.RequireEmailConfirmation).IsFalse();
        await Assert.That(hub.CreatedAt).IsEqualTo(createdAt);
        await Assert.That(hub.LastModifiedAt).IsEqualTo(lastModifiedAt);
    }

    #endregion
}
