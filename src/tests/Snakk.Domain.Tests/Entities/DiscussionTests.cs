using FluentAssertions;
using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;

namespace Snakk.Domain.Tests.Entities;

public class DiscussionTests
{
    [Fact]
    public void Create_WithValidParameters_CreatesDiscussion()
    {
        // Arrange
        var spaceId = SpaceId.New();
        var authorId = UserId.New();
        const string title = "Test Discussion";
        const string slug = "test-discussion";

        // Act
        var discussion = Discussion.Create(spaceId, authorId, title, slug);

        // Assert
        discussion.Should().NotBeNull();
        discussion.PublicId.Should().NotBe(Guid.Empty);
        discussion.SpaceId.Should().Be(spaceId);
        discussion.CreatedByUserId.Should().Be(authorId);
        discussion.Title.Should().Be(title);
        discussion.Slug.Should().Be(slug);
        discussion.IsPinned.Should().BeFalse();
        discussion.IsLocked.Should().BeFalse();
        discussion.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        discussion.LastActivityAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Create_WithEmptyTitle_ThrowsArgumentException()
    {
        // Arrange
        var spaceId = SpaceId.New();
        var authorId = UserId.New();
        const string emptyTitle = "";
        const string slug = "test-discussion";

        // Act
        var act = () => Discussion.Create(spaceId, authorId, emptyTitle, slug);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*title*");
    }

    [Fact]
    public void Create_WithWhitespaceTitle_ThrowsArgumentException()
    {
        // Arrange
        var spaceId = SpaceId.New();
        var authorId = UserId.New();
        const string whitespaceTitle = "   ";
        const string slug = "test-discussion";

        // Act
        var act = () => Discussion.Create(spaceId, authorId, whitespaceTitle, slug);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*title*");
    }

    [Fact]
    public void Create_WithEmptySlug_ThrowsArgumentException()
    {
        // Arrange
        var spaceId = SpaceId.New();
        var authorId = UserId.New();
        const string title = "Test Discussion";
        const string emptySlug = "";

        // Act
        var act = () => Discussion.Create(spaceId, authorId, title, emptySlug);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*slug*");
    }

    [Fact]
    public void Create_WithWhitespaceSlug_ThrowsArgumentException()
    {
        // Arrange
        var spaceId = SpaceId.New();
        var authorId = UserId.New();
        const string title = "Test Discussion";
        const string whitespaceSlug = "   ";

        // Act
        var act = () => Discussion.Create(spaceId, authorId, title, whitespaceSlug);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*slug*");
    }

    [Fact]
    public void Rehydrate_WithValidParameters_RestoresDiscussion()
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
        discussion.PublicId.Should().Be(publicId);
        discussion.SpaceId.Should().Be(spaceId);
        discussion.CreatedByUserId.Should().Be(authorId);
        discussion.Title.Should().Be(title);
        discussion.Slug.Should().Be(slug);
        discussion.CreatedAt.Should().Be(createdAt);
        discussion.LastActivityAt.Should().Be(lastActivityAt);
        discussion.IsPinned.Should().BeTrue();
        discussion.IsLocked.Should().BeFalse();
    }

    [Fact]
    public void UpdateTitle_WithValidTitle_UpdatesTitle()
    {
        // Arrange
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Old Title", "old-title");
        const string newTitle = "New Title";

        // Act
        discussion.UpdateTitle(newTitle);

        // Assert
        discussion.Title.Should().Be(newTitle);
    }

    [Fact]
    public void UpdateTitle_WithEmptyTitle_ThrowsArgumentException()
    {
        // Arrange
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Original Title", "original-title");
        const string emptyTitle = "";

        // Act
        var act = () => discussion.UpdateTitle(emptyTitle);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*title*");
    }

    [Fact]
    public void UpdateTitle_WhenLocked_ThrowsInvalidOperationException()
    {
        // Arrange
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Original Title", "original-title");
        discussion.Lock();
        const string newTitle = "New Title";

        // Act
        var act = () => discussion.UpdateTitle(newTitle);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*locked*");
    }

    [Fact]
    public void Pin_SetsIsPinnedToTrue()
    {
        // Arrange
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Test Discussion", "test-discussion");

        // Act
        discussion.Pin();

        // Assert
        discussion.IsPinned.Should().BeTrue();
    }

    [Fact]
    public void Unpin_SetsIsPinnedToFalse()
    {
        // Arrange
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Test Discussion", "test-discussion");
        discussion.Pin();

        // Act
        discussion.Unpin();

        // Assert
        discussion.IsPinned.Should().BeFalse();
    }

    [Fact]
    public void Lock_SetsIsLockedToTrue()
    {
        // Arrange
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Test Discussion", "test-discussion");

        // Act
        discussion.Lock();

        // Assert
        discussion.IsLocked.Should().BeTrue();
    }

    [Fact]
    public void Unlock_SetsIsLockedToFalse()
    {
        // Arrange
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Test Discussion", "test-discussion");
        discussion.Lock();

        // Act
        discussion.Unlock();

        // Assert
        discussion.IsLocked.Should().BeFalse();
    }

    [Fact]
    public void UpdateActivity_UpdatesLastActivityAt()
    {
        // Arrange
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Test Discussion", "test-discussion");
        var originalActivity = discussion.LastActivityAt;
        Thread.Sleep(10); // Small delay to ensure time difference

        // Act
        discussion.UpdateActivity();

        // Assert
        discussion.LastActivityAt!.Value.Should().BeAfter(originalActivity!.Value);
        discussion.LastActivityAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void RehydrateForList_WithMinimalParameters_RestoresDiscussion()
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
        discussion.PublicId.Should().Be(publicId);
        discussion.SpaceId.Should().Be(spaceId);
        discussion.CreatedByUserId.Should().Be(authorId);
        discussion.Title.Should().Be(title);
        discussion.Slug.Should().Be(slug);
        discussion.CreatedAt.Should().Be(createdAt);
        discussion.LastActivityAt.Should().Be(lastActivityAt);
        discussion.IsPinned.Should().BeTrue();
        discussion.IsLocked.Should().BeFalse();
    }

    [Fact]
    public void Pin_AfterUnpin_TogglesCorrectly()
    {
        // Arrange
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Test Discussion", "test-discussion");

        // Act & Assert
        discussion.IsPinned.Should().BeFalse();

        discussion.Pin();
        discussion.IsPinned.Should().BeTrue();

        discussion.Unpin();
        discussion.IsPinned.Should().BeFalse();

        discussion.Pin();
        discussion.IsPinned.Should().BeTrue();
    }

    [Fact]
    public void Lock_AfterUnlock_TogglesCorrectly()
    {
        // Arrange
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Test Discussion", "test-discussion");

        // Act & Assert
        discussion.IsLocked.Should().BeFalse();

        discussion.Lock();
        discussion.IsLocked.Should().BeTrue();

        discussion.Unlock();
        discussion.IsLocked.Should().BeFalse();

        discussion.Lock();
        discussion.IsLocked.Should().BeTrue();
    }

    [Fact]
    public void Create_GeneratesDomainEvents()
    {
        // Arrange
        var spaceId = SpaceId.New();
        var authorId = UserId.New();
        const string title = "Test Discussion";
        const string slug = "test-discussion";

        // Act
        var discussion = Discussion.Create(spaceId, authorId, title, slug);

        // Assert
        discussion.DomainEvents.Should().NotBeEmpty();
    }

    [Fact]
    public void UpdateTitle_WhenNotLocked_DoesNotThrow()
    {
        // Arrange
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Original Title", "original-title");
        const string newTitle = "Updated Title";

        // Act
        var act = () => discussion.UpdateTitle(newTitle);

        // Assert
        act.Should().NotThrow();
        discussion.Title.Should().Be(newTitle);
    }

    [Fact]
    public void UpdateActivity_CalledMultipleTimes_AlwaysUpdatesToCurrentTime()
    {
        // Arrange
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Test Discussion", "test-discussion");

        // Act & Assert
        var firstActivity = discussion.LastActivityAt;
        Thread.Sleep(10);

        discussion.UpdateActivity();
        var secondActivity = discussion.LastActivityAt;
        secondActivity!.Value.Should().BeAfter(firstActivity!.Value);
        Thread.Sleep(10);

        discussion.UpdateActivity();
        var thirdActivity = discussion.LastActivityAt;
        thirdActivity!.Value.Should().BeAfter(secondActivity!.Value);
    }
}
