using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;

namespace Snakk.Domain.Tests.Entities;

public class DiscussionTests
{
    [Test]
    public async Task Create_WithValidParameters_CreatesDiscussion()
    {
        // Arrange
        var spaceId = SpaceId.New();
        var authorId = UserId.New();
        const string title = "Test Discussion";
        const string slug = "test-discussion";

        // Act
        var discussion = Discussion.Create(spaceId, authorId, title, slug);

        // Assert
        await Assert.That(discussion).IsNotNull();
        await Assert.That(discussion.PublicId).IsNotNull();
        await Assert.That(discussion.SpaceId).IsEqualTo(spaceId);
        await Assert.That(discussion.CreatedByUserId).IsEqualTo(authorId);
        await Assert.That(discussion.Title).IsEqualTo(title);
        await Assert.That(discussion.Slug).IsEqualTo(slug);
        await Assert.That(discussion.IsPinned).IsFalse();
        await Assert.That(discussion.IsLocked).IsFalse();
        await Assert.That(discussion.CreatedAt).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
        await Assert.That(discussion.LastActivityAt!.Value).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task Create_WithEmptyTitle_ThrowsArgumentException()
    {
        // Arrange
        var spaceId = SpaceId.New();
        var authorId = UserId.New();
        const string emptyTitle = "";
        const string slug = "test-discussion";

        // Act & Assert
        await Assert.That(() => Discussion.Create(spaceId, authorId, emptyTitle, slug)).Throws<ArgumentException>();
    }

    [Test]
    public async Task Create_WithWhitespaceTitle_ThrowsArgumentException()
    {
        // Arrange
        var spaceId = SpaceId.New();
        var authorId = UserId.New();
        const string whitespaceTitle = "   ";
        const string slug = "test-discussion";

        // Act & Assert
        await Assert.That(() => Discussion.Create(spaceId, authorId, whitespaceTitle, slug)).Throws<ArgumentException>();
    }

    [Test]
    public async Task Create_WithEmptySlug_ThrowsArgumentException()
    {
        // Arrange
        var spaceId = SpaceId.New();
        var authorId = UserId.New();
        const string title = "Test Discussion";
        const string emptySlug = "";

        // Act & Assert
        await Assert.That(() => Discussion.Create(spaceId, authorId, title, emptySlug)).Throws<ArgumentException>();
    }

    [Test]
    public async Task Create_WithWhitespaceSlug_ThrowsArgumentException()
    {
        // Arrange
        var spaceId = SpaceId.New();
        var authorId = UserId.New();
        const string title = "Test Discussion";
        const string whitespaceSlug = "   ";

        // Act & Assert
        await Assert.That(() => Discussion.Create(spaceId, authorId, title, whitespaceSlug)).Throws<ArgumentException>();
    }

    [Test]
    public async Task Rehydrate_WithValidParameters_RestoresDiscussion()
    {
        // Arrange
        var publicId = DiscussionId.New();
        var spaceId = SpaceId.New();
        var authorId = UserId.New();
        const string title = "Test Discussion";
        const string slug = "test-discussion";
        var createdAt = DateTime.UtcNow.AddDays(-1);
        var lastActivityAt = DateTime.UtcNow.AddHours(-2);
        const bool isPinned = true;
        const bool isLocked = false;

        // Act
        var discussion = Discussion.Rehydrate(
            publicId,
            spaceId,
            authorId,
            title,
            slug,
            createdAt,
            null,
            lastActivityAt,
            isPinned,
            isLocked);

        // Assert
        await Assert.That(discussion.PublicId).IsEqualTo(publicId);
        await Assert.That(discussion.SpaceId).IsEqualTo(spaceId);
        await Assert.That(discussion.CreatedByUserId).IsEqualTo(authorId);
        await Assert.That(discussion.Title).IsEqualTo(title);
        await Assert.That(discussion.Slug).IsEqualTo(slug);
        await Assert.That(discussion.CreatedAt).IsEqualTo(createdAt);
        await Assert.That(discussion.LastActivityAt).IsEqualTo(lastActivityAt);
        await Assert.That(discussion.IsPinned).IsTrue();
        await Assert.That(discussion.IsLocked).IsFalse();
    }

    [Test]
    public async Task UpdateTitle_WithValidTitle_UpdatesTitle()
    {
        // Arrange
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Old Title", "old-title");
        const string newTitle = "New Title";

        // Act
        discussion.UpdateTitle(newTitle);

        // Assert
        await Assert.That(discussion.Title).IsEqualTo(newTitle);
    }

    [Test]
    public async Task UpdateTitle_WithEmptyTitle_ThrowsArgumentException()
    {
        // Arrange
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Original Title", "original-title");
        const string emptyTitle = "";

        // Act & Assert
        await Assert.That(() => discussion.UpdateTitle(emptyTitle)).Throws<ArgumentException>();
    }

    [Test]
    public async Task UpdateTitle_WhenLocked_ThrowsInvalidOperationException()
    {
        // Arrange
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Original Title", "original-title");
        discussion.Lock();
        const string newTitle = "New Title";

        // Act & Assert
        await Assert.That(() => discussion.UpdateTitle(newTitle)).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Pin_SetsIsPinnedToTrue()
    {
        // Arrange
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Test Discussion", "test-discussion");

        // Act
        discussion.Pin();

        // Assert
        await Assert.That(discussion.IsPinned).IsTrue();
    }

    [Test]
    public async Task Unpin_SetsIsPinnedToFalse()
    {
        // Arrange
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Test Discussion", "test-discussion");
        discussion.Pin();

        // Act
        discussion.Unpin();

        // Assert
        await Assert.That(discussion.IsPinned).IsFalse();
    }

    [Test]
    public async Task Lock_SetsIsLockedToTrue()
    {
        // Arrange
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Test Discussion", "test-discussion");

        // Act
        discussion.Lock();

        // Assert
        await Assert.That(discussion.IsLocked).IsTrue();
    }

    [Test]
    public async Task Unlock_SetsIsLockedToFalse()
    {
        // Arrange
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Test Discussion", "test-discussion");
        discussion.Lock();

        // Act
        discussion.Unlock();

        // Assert
        await Assert.That(discussion.IsLocked).IsFalse();
    }

    [Test]
    public async Task UpdateActivity_UpdatesLastActivityAt()
    {
        // Arrange
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Test Discussion", "test-discussion");
        var originalActivity = discussion.LastActivityAt!.Value;
        Thread.Sleep(10); // Small delay to ensure time difference

        // Act
        discussion.UpdateActivity();

        // Assert
        await Assert.That(discussion.LastActivityAt!.Value).IsGreaterThan(originalActivity);
        await Assert.That(discussion.LastActivityAt!.Value).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task RehydrateForList_WithMinimalParameters_RestoresDiscussion()
    {
        // Arrange
        var publicId = DiscussionId.New();
        var spaceId = SpaceId.New();
        var authorId = UserId.New();
        const string title = "Test Discussion";
        const string slug = "test-discussion";
        var createdAt = DateTime.UtcNow.AddDays(-1);
        var lastActivityAt = DateTime.UtcNow.AddHours(-2);
        const bool isPinned = true;
        const bool isLocked = false;

        // Act
        var discussion = Discussion.RehydrateForList(
            publicId,
            spaceId,
            authorId,
            title,
            slug,
            createdAt,
            lastActivityAt,
            isPinned,
            isLocked);

        // Assert
        await Assert.That(discussion.PublicId).IsEqualTo(publicId);
        await Assert.That(discussion.SpaceId).IsEqualTo(spaceId);
        await Assert.That(discussion.CreatedByUserId).IsEqualTo(authorId);
        await Assert.That(discussion.Title).IsEqualTo(title);
        await Assert.That(discussion.Slug).IsEqualTo(slug);
        await Assert.That(discussion.CreatedAt).IsEqualTo(createdAt);
        await Assert.That(discussion.LastActivityAt).IsEqualTo(lastActivityAt);
        await Assert.That(discussion.IsPinned).IsTrue();
        await Assert.That(discussion.IsLocked).IsFalse();
    }

    [Test]
    public async Task Pin_AfterUnpin_TogglesCorrectly()
    {
        // Arrange
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Test Discussion", "test-discussion");

        // Act & Assert
        await Assert.That(discussion.IsPinned).IsFalse();

        discussion.Pin();
        await Assert.That(discussion.IsPinned).IsTrue();

        discussion.Unpin();
        await Assert.That(discussion.IsPinned).IsFalse();

        discussion.Pin();
        await Assert.That(discussion.IsPinned).IsTrue();
    }

    [Test]
    public async Task Lock_AfterUnlock_TogglesCorrectly()
    {
        // Arrange
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Test Discussion", "test-discussion");

        // Act & Assert
        await Assert.That(discussion.IsLocked).IsFalse();

        discussion.Lock();
        await Assert.That(discussion.IsLocked).IsTrue();

        discussion.Unlock();
        await Assert.That(discussion.IsLocked).IsFalse();

        discussion.Lock();
        await Assert.That(discussion.IsLocked).IsTrue();
    }

    [Test]
    public async Task Create_GeneratesDomainEvents()
    {
        // Arrange
        var spaceId = SpaceId.New();
        var authorId = UserId.New();
        const string title = "Test Discussion";
        const string slug = "test-discussion";

        // Act
        var discussion = Discussion.Create(spaceId, authorId, title, slug);

        // Assert
        await Assert.That(discussion.DomainEvents).IsNotEmpty();
    }

    [Test]
    public async Task UpdateTitle_WhenNotLocked_DoesNotThrow()
    {
        // Arrange
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Original Title", "original-title");
        const string newTitle = "Updated Title";

        // Act & Assert
        await Assert.That(() => discussion.UpdateTitle(newTitle)).ThrowsNothing();
        await Assert.That(discussion.Title).IsEqualTo(newTitle);
    }

    [Test]
    public async Task UpdateActivity_CalledMultipleTimes_AlwaysUpdatesToCurrentTime()
    {
        // Arrange
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Test Discussion", "test-discussion");

        // Act & Assert
        var firstActivity = discussion.LastActivityAt!.Value;
        Thread.Sleep(10);

        discussion.UpdateActivity();
        var secondActivity = discussion.LastActivityAt!.Value;
        await Assert.That(secondActivity).IsGreaterThan(firstActivity);
        Thread.Sleep(10);

        discussion.UpdateActivity();
        var thirdActivity = discussion.LastActivityAt!.Value;
        await Assert.That(thirdActivity).IsGreaterThan(secondActivity);
    }
}
