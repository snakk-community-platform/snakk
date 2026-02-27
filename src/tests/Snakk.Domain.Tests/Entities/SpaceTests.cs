using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;

namespace Snakk.Domain.Tests.Entities;

public class SpaceTests
{
    #region Create Tests

    [Test]
    public async Task Create_WithValidParams_CreatesSpace()
    {
        // Arrange
        var hubId = HubId.New();

        // Act
        var space = Space.Create(hubId, "Test Space", "test-space");

        // Assert
        await Assert.That(space).IsNotNull();
        await Assert.That(space.PublicId).IsNotNull();
        await Assert.That(space.HubId).IsEqualTo(hubId);
        await Assert.That(space.Name).IsEqualTo("Test Space");
        await Assert.That(space.Slug).IsEqualTo("test-space");
        await Assert.That(space.CreatedAt).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task Create_SetsDefaults()
    {
        // Act
        var space = Space.Create(HubId.New(), "Test", "test");

        // Assert
        await Assert.That(space.AllowAnonymousReading).IsTrue();
        await Assert.That(space.RequireEmailConfirmation).IsTrue();
        await Assert.That(space.Description).IsNull();
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task Create_WithEmptyName_ThrowsArgumentException(string? emptyName)
    {
        // Act & Assert
        await Assert.That(() => Space.Create(HubId.New(), emptyName!, "test-slug")).Throws<ArgumentException>();
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task Create_WithEmptySlug_ThrowsArgumentException(string? emptySlug)
    {
        // Act & Assert
        await Assert.That(() => Space.Create(HubId.New(), "Test Name", emptySlug!)).Throws<ArgumentException>();
    }

    [Test]
    public async Task Create_WithDescription_SetsDescription()
    {
        // Act
        var space = Space.Create(HubId.New(), "Test", "test", description: "A test space");

        // Assert
        await Assert.That(space.Description).IsEqualTo("A test space");
    }

    #endregion

    #region UpdateName Tests

    [Test]
    public async Task UpdateName_UpdatesNameAndLastModifiedAt()
    {
        // Arrange
        var space = Space.Create(HubId.New(), "Original", "original");

        // Act
        space.UpdateName("Updated Name");

        // Assert
        await Assert.That(space.Name).IsEqualTo("Updated Name");
        await Assert.That(space.LastModifiedAt).IsNotNull();
        await Assert.That(space.LastModifiedAt!.Value).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task UpdateName_WithEmpty_ThrowsArgumentException(string? emptyName)
    {
        // Arrange
        var space = Space.Create(HubId.New(), "Original", "original");

        // Act & Assert
        await Assert.That(() => space.UpdateName(emptyName!)).Throws<ArgumentException>();
    }

    #endregion

    #region UpdateDescription Tests

    [Test]
    public async Task UpdateDescription_AllowsNull()
    {
        // Arrange
        var space = Space.Create(HubId.New(), "Test", "test", description: "Original");

        // Act
        space.UpdateDescription(null);

        // Assert
        await Assert.That(space.Description).IsNull();
        await Assert.That(space.LastModifiedAt).IsNotNull();
    }

    [Test]
    public async Task UpdateDescription_SetsNewDescription()
    {
        // Arrange
        var space = Space.Create(HubId.New(), "Test", "test");

        // Act
        space.UpdateDescription("New description");

        // Assert
        await Assert.That(space.Description).IsEqualTo("New description");
    }

    #endregion

    #region UpdateSlug Tests

    [Test]
    public async Task UpdateSlug_UpdatesSlug()
    {
        // Arrange
        var space = Space.Create(HubId.New(), "Test", "original-slug");

        // Act
        space.UpdateSlug("new-slug");

        // Assert
        await Assert.That(space.Slug).IsEqualTo("new-slug");
        await Assert.That(space.LastModifiedAt).IsNotNull();
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task UpdateSlug_WithEmpty_ThrowsArgumentException(string? emptySlug)
    {
        // Arrange
        var space = Space.Create(HubId.New(), "Test", "original");

        // Act & Assert
        await Assert.That(() => space.UpdateSlug(emptySlug!)).Throws<ArgumentException>();
    }

    #endregion

    #region Discussions Collection Tests

    [Test]
    public async Task Discussions_InitiallyEmpty()
    {
        // Arrange & Act
        var space = Space.Create(HubId.New(), "Test", "test");

        // Assert
        await Assert.That(space.Discussions).IsEmpty();
    }

    #endregion

    #region Rehydrate Tests

    [Test]
    public async Task Rehydrate_RestoresAllProperties()
    {
        // Arrange
        var publicId = SpaceId.New();
        var hubId = HubId.New();
        var createdAt = DateTime.UtcNow.AddDays(-3);
        var lastModifiedAt = DateTime.UtcNow.AddMinutes(-30);

        // Act
        var space = Space.Rehydrate(
            publicId,
            hubId,
            "Rehydrated Space",
            "rehydrated-space",
            "Space description",
            allowAnonymousReading: false,
            requireEmailConfirmation: false,
            createdAt,
            lastModifiedAt);

        // Assert
        await Assert.That(space.PublicId).IsEqualTo(publicId);
        await Assert.That(space.HubId).IsEqualTo(hubId);
        await Assert.That(space.Name).IsEqualTo("Rehydrated Space");
        await Assert.That(space.Slug).IsEqualTo("rehydrated-space");
        await Assert.That(space.Description).IsEqualTo("Space description");
        await Assert.That(space.AllowAnonymousReading).IsFalse();
        await Assert.That(space.RequireEmailConfirmation).IsFalse();
        await Assert.That(space.CreatedAt).IsEqualTo(createdAt);
        await Assert.That(space.LastModifiedAt).IsEqualTo(lastModifiedAt);
    }

    #endregion
}
