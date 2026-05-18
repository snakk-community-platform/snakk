using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;

namespace Snakk.Domain.Tests.Entities;

public class GroupTests
{
    #region Create Tests

    [Test]
    public async Task Create_WithValidParameters_CreatesGroup()
    {
        // Arrange
        var communityId = CommunityId.New();
        const string name = "Test Group";
        const string slug = "test-group";
        const string description = "A test group";

        // Act
        var group = Group.Create(communityId, name, slug, description, isPublic: true, sortOrder: 5);

        // Assert
        await Assert.That(group).IsNotNull();
        await Assert.That(group.PublicId).IsNotNull();
        await Assert.That((string)group.CommunityId).IsEqualTo((string)communityId);
        await Assert.That(group.Name).IsEqualTo(name);
        await Assert.That(group.Slug).IsEqualTo(slug);
        await Assert.That(group.Description).IsEqualTo(description);
        await Assert.That(group.IsPublic).IsTrue();
        await Assert.That(group.SortOrder).IsEqualTo(5);
        await Assert.That(group.CreatedAt).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
        await Assert.That(group.UpdatedAt).IsNull();
    }

    [Test]
    public async Task Create_WithDefaultOptionalParameters_UsesDefaults()
    {
        // Act
        var group = Group.Create(CommunityId.New(), "My Group", "my-group");

        // Assert
        await Assert.That(group.Description).IsNull();
        await Assert.That(group.IsPublic).IsTrue();
        await Assert.That(group.SortOrder).IsEqualTo(0);
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task Create_WithInvalidName_ThrowsArgumentException(string? invalidName)
    {
        // Act & Assert
        await Assert.That(() => Group.Create(CommunityId.New(), invalidName!, "valid-slug")).Throws<ArgumentException>();
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task Create_WithInvalidSlug_ThrowsArgumentException(string? invalidSlug)
    {
        // Act & Assert
        await Assert.That(() => Group.Create(CommunityId.New(), "Valid Name", invalidSlug!)).Throws<ArgumentException>();
    }

    #endregion

    #region Rehydrate Tests

    [Test]
    public async Task Rehydrate_WithAllParameters_CreatesGroupWithExactState()
    {
        // Arrange
        var groupId = GroupId.New();
        var communityId = CommunityId.New();
        var createdAt = DateTime.UtcNow.AddDays(-10);
        var updatedAt = DateTime.UtcNow.AddDays(-2);

        // Act
        var group = Group.Rehydrate(groupId, communityId, "My Group", "my-group", "desc", false, 3, createdAt, updatedAt);

        // Assert
        await Assert.That((string)group.PublicId).IsEqualTo((string)groupId);
        await Assert.That((string)group.CommunityId).IsEqualTo((string)communityId);
        await Assert.That(group.Name).IsEqualTo("My Group");
        await Assert.That(group.Slug).IsEqualTo("my-group");
        await Assert.That(group.Description).IsEqualTo("desc");
        await Assert.That(group.IsPublic).IsFalse();
        await Assert.That(group.SortOrder).IsEqualTo(3);
        await Assert.That(group.CreatedAt).IsEqualTo(createdAt);
        await Assert.That(group.UpdatedAt).IsEqualTo(updatedAt);
    }

    #endregion

    #region UpdateName Tests

    [Test]
    public async Task UpdateName_WithValidName_UpdatesNameAndSetsUpdatedAt()
    {
        // Arrange
        var group = Group.Create(CommunityId.New(), "Original Name", "original-slug");

        // Act
        group.UpdateName("New Name");

        // Assert
        await Assert.That(group.Name).IsEqualTo("New Name");
        await Assert.That(group.UpdatedAt).IsNotNull();
        await Assert.That(group.UpdatedAt!.Value).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task UpdateName_WithInvalidName_ThrowsArgumentException(string? invalidName)
    {
        // Arrange
        var group = Group.Create(CommunityId.New(), "Valid Name", "valid-slug");

        // Act & Assert
        await Assert.That(() => group.UpdateName(invalidName!)).Throws<ArgumentException>();
    }

    #endregion

    #region UpdateDescription Tests

    [Test]
    public async Task UpdateDescription_WithValidString_UpdatesDescriptionAndSetsUpdatedAt()
    {
        // Arrange
        var group = Group.Create(CommunityId.New(), "Group", "group");

        // Act
        group.UpdateDescription("New description");

        // Assert
        await Assert.That(group.Description).IsEqualTo("New description");
        await Assert.That(group.UpdatedAt!.Value).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task UpdateDescription_WithNull_ClearsDescription()
    {
        // Arrange
        var group = Group.Create(CommunityId.New(), "Group", "group", description: "initial");

        // Act
        group.UpdateDescription(null);

        // Assert
        await Assert.That(group.Description).IsNull();
        await Assert.That(group.UpdatedAt).IsNotNull();
    }

    #endregion

    #region SetPublic Tests

    [Test]
    public async Task SetPublic_WithTrue_SetsIsPublicAndUpdatedAt()
    {
        // Arrange
        var group = Group.Create(CommunityId.New(), "Group", "group", isPublic: false);

        // Act
        group.SetPublic(true);

        // Assert
        await Assert.That(group.IsPublic).IsTrue();
        await Assert.That(group.UpdatedAt!.Value).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task SetPublic_WithFalse_SetsIsPublicFalseAndUpdatedAt()
    {
        // Arrange
        var group = Group.Create(CommunityId.New(), "Group", "group", isPublic: true);

        // Act
        group.SetPublic(false);

        // Assert
        await Assert.That(group.IsPublic).IsFalse();
        await Assert.That(group.UpdatedAt!.Value).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
    }

    #endregion

    #region UpdateSortOrder Tests

    [Test]
    public async Task UpdateSortOrder_WithNewValue_UpdatesSortOrderAndSetsUpdatedAt()
    {
        // Arrange
        var group = Group.Create(CommunityId.New(), "Group", "group", sortOrder: 0);

        // Act
        group.UpdateSortOrder(42);

        // Assert
        await Assert.That(group.SortOrder).IsEqualTo(42);
        await Assert.That(group.UpdatedAt!.Value).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task UpdateSortOrder_WithNegativeValue_AcceptsNegativeOrder()
    {
        // Arrange
        var group = Group.Create(CommunityId.New(), "Group", "group");

        // Act
        group.UpdateSortOrder(-1);

        // Assert
        await Assert.That(group.SortOrder).IsEqualTo(-1);
    }

    #endregion
}
