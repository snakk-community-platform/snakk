using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Enums;

namespace Snakk.Domain.Tests.Entities;

public class AnnouncementTests
{
    [Test]
    public async Task Create_WithValidParameters_CreatesAnnouncement()
    {
        // Arrange
        var userId = UserId.New();

        // Act
        var announcement = Announcement.Create(
            AnnouncementScopeEnum.Community,
            "community-id-123",
            userId,
            "Welcome",
            "Welcome to the community!",
            "<p>Welcome to the community!</p>");

        // Assert
        await Assert.That(announcement).IsNotNull();
        await Assert.That(announcement.PublicId).IsNotNull();
        await Assert.That(announcement.Title).IsEqualTo("Welcome");
        await Assert.That(announcement.Content).IsEqualTo("Welcome to the community!");
        await Assert.That(announcement.RenderedContent).IsEqualTo("<p>Welcome to the community!</p>");
        await Assert.That(announcement.Type).IsEqualTo(AnnouncementTypeEnum.Info);
        await Assert.That(announcement.Scope).IsEqualTo(AnnouncementScopeEnum.Community);
        await Assert.That(announcement.ScopeEntityId).IsEqualTo("community-id-123");
        await Assert.That(announcement.IsDismissible).IsTrue();
        await Assert.That(announcement.SortOrder).IsEqualTo(0);
        await Assert.That(announcement.CreatedByUserId).IsEqualTo(userId);
        await Assert.That(announcement.CreatedAt).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
        await Assert.That(announcement.LastModifiedAt).IsNull();
    }

    [Test]
    public async Task Create_WithAllParameters_SetsCorrectly()
    {
        // Arrange
        var userId = UserId.New();
        var visibleFrom = DateTime.UtcNow.AddDays(1);
        var visibleUntil = DateTime.UtcNow.AddDays(7);

        // Act
        var announcement = Announcement.Create(
            AnnouncementScopeEnum.Hub,
            "hub-id-456",
            userId,
            "Maintenance Notice",
            "Server maintenance scheduled",
            "<p>Server maintenance scheduled</p>",
            AnnouncementTypeEnum.Warning,
            visibleFrom,
            visibleUntil,
            isDismissible: false,
            sortOrder: 5);

        // Assert
        await Assert.That(announcement.Type).IsEqualTo(AnnouncementTypeEnum.Warning);
        await Assert.That(announcement.Scope).IsEqualTo(AnnouncementScopeEnum.Hub);
        await Assert.That(announcement.VisibleFrom).IsEqualTo(visibleFrom);
        await Assert.That(announcement.VisibleUntil).IsEqualTo(visibleUntil);
        await Assert.That(announcement.IsDismissible).IsFalse();
        await Assert.That(announcement.SortOrder).IsEqualTo(5);
    }

    [Test]
    public async Task Create_WithEmptyTitle_ThrowsArgumentException()
    {
        var userId = UserId.New();

        await Assert.That(() => Announcement.Create(
            AnnouncementScopeEnum.Community,
            "community-id",
            userId,
            "",
            "Some content",
            "<p>Some content</p>")).Throws<ArgumentException>();
    }

    [Test]
    public async Task Create_WithEmptyContent_ThrowsArgumentException()
    {
        var userId = UserId.New();

        await Assert.That(() => Announcement.Create(
            AnnouncementScopeEnum.Community,
            "community-id",
            userId,
            "Title",
            "",
            "")).Throws<ArgumentException>();
    }

    [Test]
    public async Task Create_WithEmptyScopeEntityId_ThrowsArgumentException()
    {
        var userId = UserId.New();

        await Assert.That(() => Announcement.Create(
            AnnouncementScopeEnum.Community,
            "",
            userId,
            "Title",
            "Content",
            "<p>Content</p>")).Throws<ArgumentException>();
    }

    [Test]
    public async Task Create_WithVisibleFromAfterVisibleUntil_ThrowsArgumentException()
    {
        var userId = UserId.New();

        await Assert.That(() => Announcement.Create(
            AnnouncementScopeEnum.Community,
            "community-id",
            userId,
            "Title",
            "Content",
            "<p>Content</p>",
            visibleFrom: DateTime.UtcNow.AddDays(7),
            visibleUntil: DateTime.UtcNow.AddDays(1))).Throws<ArgumentException>();
    }

    [Test]
    public async Task Update_ChangesPropertiesAndSetsLastModifiedAt()
    {
        // Arrange
        var announcement = CreateTestAnnouncement();

        // Act
        announcement.Update(
            "Updated Title",
            "Updated content",
            "<p>Updated content</p>",
            AnnouncementTypeEnum.Critical,
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(30),
            isDismissible: false,
            sortOrder: 10);

        // Assert
        await Assert.That(announcement.Title).IsEqualTo("Updated Title");
        await Assert.That(announcement.Content).IsEqualTo("Updated content");
        await Assert.That(announcement.RenderedContent).IsEqualTo("<p>Updated content</p>");
        await Assert.That(announcement.Type).IsEqualTo(AnnouncementTypeEnum.Critical);
        await Assert.That(announcement.IsDismissible).IsFalse();
        await Assert.That(announcement.SortOrder).IsEqualTo(10);
        await Assert.That(announcement.LastModifiedAt).IsNotNull();
        await Assert.That(announcement.LastModifiedAt!.Value).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task Update_WithEmptyTitle_ThrowsArgumentException()
    {
        var announcement = CreateTestAnnouncement();

        await Assert.That(() => announcement.Update(
            "",
            "Content",
            "<p>Content</p>",
            AnnouncementTypeEnum.Info,
            null, null, true, 0)).Throws<ArgumentException>();
    }

    [Test]
    public async Task IsCurrentlyVisible_WithNoDateRange_ReturnsTrue()
    {
        var announcement = CreateTestAnnouncement();

        await Assert.That(announcement.IsCurrentlyVisible()).IsTrue();
    }

    [Test]
    public async Task IsCurrentlyVisible_BeforeVisibleFrom_ReturnsFalse()
    {
        var announcement = Announcement.Create(
            AnnouncementScopeEnum.Community,
            "community-id",
            UserId.New(),
            "Future",
            "Future content",
            "<p>Future content</p>",
            visibleFrom: DateTime.UtcNow.AddDays(1));

        await Assert.That(announcement.IsCurrentlyVisible()).IsFalse();
    }

    [Test]
    public async Task IsCurrentlyVisible_AfterVisibleUntil_ReturnsFalse()
    {
        var announcement = Announcement.Rehydrate(
            AnnouncementId.New(),
            "Expired",
            "Expired content",
            "<p>Expired content</p>",
            AnnouncementTypeEnum.Info,
            AnnouncementScopeEnum.Community,
            "community-id",
            visibleFrom: DateTime.UtcNow.AddDays(-10),
            visibleUntil: DateTime.UtcNow.AddDays(-1),
            isDismissible: true,
            sortOrder: 0,
            UserId.New(),
            DateTime.UtcNow.AddDays(-10),
            lastModifiedAt: null);

        await Assert.That(announcement.IsCurrentlyVisible()).IsFalse();
    }

    [Test]
    public async Task IsCurrentlyVisible_WithinDateRange_ReturnsTrue()
    {
        var announcement = Announcement.Create(
            AnnouncementScopeEnum.Community,
            "community-id",
            UserId.New(),
            "Active",
            "Active content",
            "<p>Active content</p>",
            visibleFrom: DateTime.UtcNow.AddDays(-1),
            visibleUntil: DateTime.UtcNow.AddDays(1));

        await Assert.That(announcement.IsCurrentlyVisible()).IsTrue();
    }

    [Test]
    public async Task Rehydrate_RestoresAllProperties()
    {
        // Arrange
        var publicId = AnnouncementId.New();
        var userId = UserId.New();
        var createdAt = DateTime.UtcNow.AddDays(-5);
        var lastModifiedAt = DateTime.UtcNow.AddDays(-1);
        var visibleFrom = DateTime.UtcNow.AddDays(-3);
        var visibleUntil = DateTime.UtcNow.AddDays(3);

        // Act
        var announcement = Announcement.Rehydrate(
            publicId,
            "Rehydrated Title",
            "Rehydrated content",
            "<p>Rehydrated content</p>",
            AnnouncementTypeEnum.Warning,
            AnnouncementScopeEnum.Space,
            "space-id-789",
            visibleFrom,
            visibleUntil,
            isDismissible: false,
            sortOrder: 3,
            userId,
            createdAt,
            lastModifiedAt);

        // Assert
        await Assert.That(announcement.PublicId).IsEqualTo(publicId);
        await Assert.That(announcement.Title).IsEqualTo("Rehydrated Title");
        await Assert.That(announcement.Content).IsEqualTo("Rehydrated content");
        await Assert.That(announcement.RenderedContent).IsEqualTo("<p>Rehydrated content</p>");
        await Assert.That(announcement.Type).IsEqualTo(AnnouncementTypeEnum.Warning);
        await Assert.That(announcement.Scope).IsEqualTo(AnnouncementScopeEnum.Space);
        await Assert.That(announcement.ScopeEntityId).IsEqualTo("space-id-789");
        await Assert.That(announcement.VisibleFrom).IsEqualTo(visibleFrom);
        await Assert.That(announcement.VisibleUntil).IsEqualTo(visibleUntil);
        await Assert.That(announcement.IsDismissible).IsFalse();
        await Assert.That(announcement.SortOrder).IsEqualTo(3);
        await Assert.That(announcement.CreatedByUserId).IsEqualTo(userId);
        await Assert.That(announcement.CreatedAt).IsEqualTo(createdAt);
        await Assert.That(announcement.LastModifiedAt).IsEqualTo(lastModifiedAt);
    }

    private static Announcement CreateTestAnnouncement() =>
        Announcement.Create(
            AnnouncementScopeEnum.Community,
            "community-id",
            UserId.New(),
            "Test Announcement",
            "Test content here",
            "<p>Test content here</p>");
}
