using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Enums;

namespace Snakk.Domain.Tests.Entities;

public class BannerTests
{
    [Test]
    public async Task Create_WithValidParameters_CreatesBanner()
    {
        // Arrange
        var userId = UserId.New();

        // Act
        var banner = Banner.Create(
            BannerScopeEnum.Community,
            "community-id-123",
            userId,
            "Welcome",
            "Welcome to the community!",
            "<p>Welcome to the community!</p>");

        // Assert
        await Assert.That(banner).IsNotNull();
        await Assert.That(banner.PublicId).IsNotNull();
        await Assert.That(banner.Title).IsEqualTo("Welcome");
        await Assert.That(banner.Content).IsEqualTo("Welcome to the community!");
        await Assert.That(banner.RenderedContent).IsEqualTo("<p>Welcome to the community!</p>");
        await Assert.That(banner.Type).IsEqualTo(BannerTypeEnum.Info);
        await Assert.That(banner.Scope).IsEqualTo(BannerScopeEnum.Community);
        await Assert.That(banner.ScopeEntityId).IsEqualTo("community-id-123");
        await Assert.That(banner.IsDismissible).IsTrue();
        await Assert.That(banner.SortOrder).IsEqualTo(0);
        await Assert.That(banner.CreatedByUserId).IsEqualTo(userId);
        await Assert.That(banner.CreatedAt).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
        await Assert.That(banner.LastModifiedAt).IsNull();
    }

    [Test]
    public async Task Create_WithAllParameters_SetsCorrectly()
    {
        // Arrange
        var userId = UserId.New();
        var visibleFrom = DateTime.UtcNow.AddDays(1);
        var visibleUntil = DateTime.UtcNow.AddDays(7);

        // Act
        var banner = Banner.Create(
            BannerScopeEnum.Hub,
            "hub-id-456",
            userId,
            "Maintenance Notice",
            "Server maintenance scheduled",
            "<p>Server maintenance scheduled</p>",
            BannerTypeEnum.Warning,
            visibleFrom,
            visibleUntil,
            isDismissible: false,
            sortOrder: 5);

        // Assert
        await Assert.That(banner.Type).IsEqualTo(BannerTypeEnum.Warning);
        await Assert.That(banner.Scope).IsEqualTo(BannerScopeEnum.Hub);
        await Assert.That(banner.VisibleFrom).IsEqualTo(visibleFrom);
        await Assert.That(banner.VisibleUntil).IsEqualTo(visibleUntil);
        await Assert.That(banner.IsDismissible).IsFalse();
        await Assert.That(banner.SortOrder).IsEqualTo(5);
    }

    [Test]
    public async Task Create_WithEmptyTitle_ThrowsArgumentException()
    {
        var userId = UserId.New();

        await Assert.That(() => Banner.Create(
            BannerScopeEnum.Community,
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

        await Assert.That(() => Banner.Create(
            BannerScopeEnum.Community,
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

        await Assert.That(() => Banner.Create(
            BannerScopeEnum.Community,
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

        await Assert.That(() => Banner.Create(
            BannerScopeEnum.Community,
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
        var banner = CreateTestBanner();

        // Act
        banner.Update(
            "Updated Title",
            "Updated content",
            "<p>Updated content</p>",
            BannerTypeEnum.Critical,
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(30),
            isDismissible: false,
            sortOrder: 10);

        // Assert
        await Assert.That(banner.Title).IsEqualTo("Updated Title");
        await Assert.That(banner.Content).IsEqualTo("Updated content");
        await Assert.That(banner.RenderedContent).IsEqualTo("<p>Updated content</p>");
        await Assert.That(banner.Type).IsEqualTo(BannerTypeEnum.Critical);
        await Assert.That(banner.IsDismissible).IsFalse();
        await Assert.That(banner.SortOrder).IsEqualTo(10);
        await Assert.That(banner.LastModifiedAt).IsNotNull();
        await Assert.That(banner.LastModifiedAt!.Value).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task Update_WithEmptyTitle_ThrowsArgumentException()
    {
        var banner = CreateTestBanner();

        await Assert.That(() => banner.Update(
            "",
            "Content",
            "<p>Content</p>",
            BannerTypeEnum.Info,
            null, null, true, 0)).Throws<ArgumentException>();
    }

    [Test]
    public async Task IsCurrentlyVisible_WithNoDateRange_ReturnsTrue()
    {
        var banner = CreateTestBanner();

        await Assert.That(banner.IsCurrentlyVisible()).IsTrue();
    }

    [Test]
    public async Task IsCurrentlyVisible_BeforeVisibleFrom_ReturnsFalse()
    {
        var banner = Banner.Create(
            BannerScopeEnum.Community,
            "community-id",
            UserId.New(),
            "Future",
            "Future content",
            "<p>Future content</p>",
            visibleFrom: DateTime.UtcNow.AddDays(1));

        await Assert.That(banner.IsCurrentlyVisible()).IsFalse();
    }

    [Test]
    public async Task IsCurrentlyVisible_AfterVisibleUntil_ReturnsFalse()
    {
        var banner = Banner.Rehydrate(
            BannerId.New(),
            "Expired",
            "Expired content",
            "<p>Expired content</p>",
            BannerTypeEnum.Info,
            BannerScopeEnum.Community,
            "community-id",
            visibleFrom: DateTime.UtcNow.AddDays(-10),
            visibleUntil: DateTime.UtcNow.AddDays(-1),
            isDismissible: true,
            sortOrder: 0,
            UserId.New(),
            DateTime.UtcNow.AddDays(-10),
            lastModifiedAt: null);

        await Assert.That(banner.IsCurrentlyVisible()).IsFalse();
    }

    [Test]
    public async Task IsCurrentlyVisible_WithinDateRange_ReturnsTrue()
    {
        var banner = Banner.Create(
            BannerScopeEnum.Community,
            "community-id",
            UserId.New(),
            "Active",
            "Active content",
            "<p>Active content</p>",
            visibleFrom: DateTime.UtcNow.AddDays(-1),
            visibleUntil: DateTime.UtcNow.AddDays(1));

        await Assert.That(banner.IsCurrentlyVisible()).IsTrue();
    }

    [Test]
    public async Task Rehydrate_RestoresAllProperties()
    {
        // Arrange
        var publicId = BannerId.New();
        var userId = UserId.New();
        var createdAt = DateTime.UtcNow.AddDays(-5);
        var lastModifiedAt = DateTime.UtcNow.AddDays(-1);
        var visibleFrom = DateTime.UtcNow.AddDays(-3);
        var visibleUntil = DateTime.UtcNow.AddDays(3);

        // Act
        var banner = Banner.Rehydrate(
            publicId,
            "Rehydrated Title",
            "Rehydrated content",
            "<p>Rehydrated content</p>",
            BannerTypeEnum.Warning,
            BannerScopeEnum.Space,
            "space-id-789",
            visibleFrom,
            visibleUntil,
            isDismissible: false,
            sortOrder: 3,
            userId,
            createdAt,
            lastModifiedAt);

        // Assert
        await Assert.That(banner.PublicId).IsEqualTo(publicId);
        await Assert.That(banner.Title).IsEqualTo("Rehydrated Title");
        await Assert.That(banner.Content).IsEqualTo("Rehydrated content");
        await Assert.That(banner.RenderedContent).IsEqualTo("<p>Rehydrated content</p>");
        await Assert.That(banner.Type).IsEqualTo(BannerTypeEnum.Warning);
        await Assert.That(banner.Scope).IsEqualTo(BannerScopeEnum.Space);
        await Assert.That(banner.ScopeEntityId).IsEqualTo("space-id-789");
        await Assert.That(banner.VisibleFrom).IsEqualTo(visibleFrom);
        await Assert.That(banner.VisibleUntil).IsEqualTo(visibleUntil);
        await Assert.That(banner.IsDismissible).IsFalse();
        await Assert.That(banner.SortOrder).IsEqualTo(3);
        await Assert.That(banner.CreatedByUserId).IsEqualTo(userId);
        await Assert.That(banner.CreatedAt).IsEqualTo(createdAt);
        await Assert.That(banner.LastModifiedAt).IsEqualTo(lastModifiedAt);
    }

    private static Banner CreateTestBanner() =>
        Banner.Create(
            BannerScopeEnum.Community,
            "community-id",
            UserId.New(),
            "Test Banner",
            "Test content here",
            "<p>Test content here</p>");
}
