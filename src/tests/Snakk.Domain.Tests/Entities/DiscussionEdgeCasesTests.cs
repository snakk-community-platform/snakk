using Snakk.Domain.Entities;
using Snakk.Domain.Events;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Enums;

namespace Snakk.Domain.Tests.Entities;

public class DiscussionEdgeCasesTests
{
    #region Pin Idempotency

    [Test]
    public async Task Pin_AlreadyPinnedDiscussion_RemainsIdempotent()
    {
        // Arrange
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Test Discussion", "test-discussion");
        discussion.Pin();
        await Assert.That(discussion.IsPinned).IsTrue();
        var firstModifiedAt = discussion.LastModifiedAt;
        Thread.Sleep(10);

        // Act - pin again
        discussion.Pin();

        // Assert - still pinned, LastModifiedAt updated
        await Assert.That(discussion.IsPinned).IsTrue();
        await Assert.That(discussion.LastModifiedAt).IsNotEqualTo(firstModifiedAt);
    }

    [Test]
    public async Task Pin_AlreadyPinned_DoesNotThrow()
    {
        // Arrange
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Test", "test");
        discussion.Pin();

        // Act & Assert
        await Assert.That(() => discussion.Pin()).ThrowsNothing();
    }

    #endregion

    #region Unpin Idempotency

    [Test]
    public async Task Unpin_AlreadyUnpinnedDiscussion_RemainsIdempotent()
    {
        // Arrange
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Test Discussion", "test-discussion");
        await Assert.That(discussion.IsPinned).IsFalse();
        var firstModifiedAt = discussion.LastModifiedAt;
        Thread.Sleep(10);

        // Act - unpin when already not pinned
        discussion.Unpin();

        // Assert - still not pinned, LastModifiedAt updated
        await Assert.That(discussion.IsPinned).IsFalse();
        await Assert.That(discussion.LastModifiedAt).IsNotEqualTo(firstModifiedAt);
    }

    [Test]
    public async Task Unpin_AlreadyUnpinned_DoesNotThrow()
    {
        // Arrange
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Test", "test");

        // Act & Assert
        await Assert.That(() => discussion.Unpin()).ThrowsNothing();
    }

    #endregion

    #region Lock Idempotency

    [Test]
    public async Task Lock_AlreadyLockedDiscussion_RemainsIdempotent()
    {
        // Arrange
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Test Discussion", "test-discussion");
        discussion.Lock();
        await Assert.That(discussion.IsLocked).IsTrue();
        var firstModifiedAt = discussion.LastModifiedAt;
        Thread.Sleep(10);

        // Act - lock again
        discussion.Lock();

        // Assert - still locked, LastModifiedAt updated
        await Assert.That(discussion.IsLocked).IsTrue();
        await Assert.That(discussion.LastModifiedAt).IsNotEqualTo(firstModifiedAt);
    }

    [Test]
    public async Task Lock_AlreadyLocked_DoesNotThrow()
    {
        // Arrange
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Test", "test");
        discussion.Lock();

        // Act & Assert
        await Assert.That(() => discussion.Lock()).ThrowsNothing();
    }

    #endregion

    #region Unlock Idempotency

    [Test]
    public async Task Unlock_AlreadyUnlockedDiscussion_RemainsIdempotent()
    {
        // Arrange
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Test Discussion", "test-discussion");
        await Assert.That(discussion.IsLocked).IsFalse();
        var firstModifiedAt = discussion.LastModifiedAt;
        Thread.Sleep(10);

        // Act - unlock when already not locked
        discussion.Unlock();

        // Assert - still not locked, LastModifiedAt updated
        await Assert.That(discussion.IsLocked).IsFalse();
        await Assert.That(discussion.LastModifiedAt).IsNotEqualTo(firstModifiedAt);
    }

    [Test]
    public async Task Unlock_AlreadyUnlocked_DoesNotThrow()
    {
        // Arrange
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Test", "test");

        // Act & Assert
        await Assert.That(() => discussion.Unlock()).ThrowsNothing();
    }

    #endregion

    #region UpdateTitle While Locked

    [Test]
    public async Task UpdateTitle_WhileLocked_ThrowsInvalidOperationException()
    {
        // Arrange
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Original Title", "original-title");
        discussion.Lock();

        // Act & Assert
        await Assert.That(() => discussion.UpdateTitle("New Title")).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task UpdateTitle_AfterLockThenUnlock_Succeeds()
    {
        // Arrange
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Original Title", "original-title");
        discussion.Lock();
        discussion.Unlock();

        // Act
        discussion.UpdateTitle("New Title After Unlock");

        // Assert
        await Assert.That(discussion.Title).IsEqualTo("New Title After Unlock");
    }

    [Test]
    public async Task UpdateTitle_WhileLocked_PreservesOriginalTitle()
    {
        // Arrange
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Original Title", "original-title");
        discussion.Lock();

        // Act
        try
        {
            discussion.UpdateTitle("Should Not Change");
        }
        catch (InvalidOperationException)
        {
            // Expected
        }

        // Assert - title should remain unchanged
        await Assert.That(discussion.Title).IsEqualTo("Original Title");
    }

    #endregion

    #region UpdateActivity

    [Test]
    public async Task UpdateActivity_UpdatesLastActivityAt()
    {
        // Arrange
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Test Discussion", "test");
        var initialActivity = discussion.LastActivityAt;
        Thread.Sleep(10);

        // Act
        discussion.UpdateActivity();

        // Assert
        await Assert.That(discussion.LastActivityAt!.Value).IsGreaterThan(initialActivity!.Value);
        await Assert.That(discussion.LastActivityAt!.Value).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task UpdateActivity_CalledRapidly_AlwaysUpdates()
    {
        // Arrange
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Test Discussion", "test");

        var timestamps = new List<DateTime>();

        // Act
        for (var i = 0; i < 5; i++)
        {
            Thread.Sleep(10);
            discussion.UpdateActivity();
            timestamps.Add(discussion.LastActivityAt!.Value);
        }

        // Assert - each timestamp should be at or after the previous
        for (var i = 1; i < timestamps.Count; i++)
        {
            await Assert.That(timestamps[i]).IsGreaterThanOrEqualTo(timestamps[i - 1]);
        }
    }

    #endregion

    #region Created With Correct Initial LastActivityAt

    [Test]
    public async Task Create_SetsInitialLastActivityAt()
    {
        // Arrange & Act
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Test", "test");

        // Assert
        await Assert.That(discussion.LastActivityAt).IsNotNull();
        await Assert.That(discussion.LastActivityAt!.Value).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task Create_LastActivityAtMatchesCreatedAt()
    {
        // Arrange & Act
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Test", "test");

        // Assert - both should be set to approximately the same time
        await Assert.That(discussion.LastActivityAt!.Value)
            .IsEqualTo(discussion.CreatedAt).Within(TimeSpan.FromMilliseconds(50));
    }

    #endregion

    #region ClearDomainEvents

    [Test]
    public async Task ClearDomainEvents_RemovesAllEvents()
    {
        // Arrange
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Test", "test");
        await Assert.That(discussion.DomainEvents).IsNotEmpty();

        // Act
        discussion.ClearDomainEvents();

        // Assert
        await Assert.That(discussion.DomainEvents).IsEmpty();
    }

    [Test]
    public async Task ClearDomainEvents_CalledOnEmpty_DoesNotThrow()
    {
        // Arrange
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Test", "test");
        discussion.ClearDomainEvents();
        await Assert.That(discussion.DomainEvents).IsEmpty();

        // Act & Assert
        await Assert.That(() => discussion.ClearDomainEvents()).ThrowsNothing();
    }

    [Test]
    public async Task Create_GeneratesDiscussionCreatedEvent()
    {
        // Arrange & Act
        var spaceId = SpaceId.New();
        var userId = UserId.New();
        var discussion = Discussion.Create(spaceId, userId, "Test", "test");

        // Assert
        await Assert.That(discussion.DomainEvents).Count().IsEqualTo(1);
        var createdEvent = discussion.DomainEvents.First() as DiscussionCreatedEvent;
        await Assert.That(createdEvent).IsNotNull();
        await Assert.That(createdEvent!.DiscussionId).IsEqualTo(discussion.PublicId);
        await Assert.That(createdEvent.SpaceId).IsEqualTo(spaceId);
        await Assert.That(createdEvent.CreatedByUserId).IsEqualTo(userId);
    }

    #endregion

    #region RehydrateForList

    [Test]
    public async Task RehydrateForList_CreatesLightweightDiscussion()
    {
        // Arrange
        var publicId = DiscussionId.New();
        var spaceId = SpaceId.New();
        var userId = UserId.New();
        var createdAt = DateTime.UtcNow.AddDays(-5);
        var lastActivityAt = DateTime.UtcNow.AddHours(-1);

        // Act
        var discussion = Discussion.RehydrateForList(
            publicId, spaceId, userId,
            "List Discussion", "list-discussion",
            DiscussionTypeEnum.Standard,
            createdAt, lastActivityAt,
            isPinned: true, isLocked: false);

        // Assert
        await Assert.That(discussion.PublicId).IsEqualTo(publicId);
        await Assert.That(discussion.SpaceId).IsEqualTo(spaceId);
        await Assert.That(discussion.CreatedByUserId).IsEqualTo(userId);
        await Assert.That(discussion.Title).IsEqualTo("List Discussion");
        await Assert.That(discussion.Slug).IsEqualTo("list-discussion");
        await Assert.That(discussion.IsPinned).IsTrue();
        await Assert.That(discussion.IsLocked).IsFalse();
        await Assert.That(discussion.LastModifiedAt).IsNull();
        await Assert.That(discussion.Posts).IsEmpty();
    }

    [Test]
    public async Task RehydrateForList_HasEmptyPostsCollection()
    {
        // Arrange & Act
        var discussion = Discussion.RehydrateForList(
            DiscussionId.New(), SpaceId.New(), UserId.New(),
            "Test", "test", DiscussionTypeEnum.Standard,
            DateTime.UtcNow, DateTime.UtcNow,
            false, false);

        // Assert
        await Assert.That(discussion.Posts).IsEmpty();
    }

    [Test]
    public async Task RehydrateForList_HasNoDomainEvents()
    {
        // Arrange & Act
        var discussion = Discussion.RehydrateForList(
            DiscussionId.New(), SpaceId.New(), UserId.New(),
            "Test", "test", DiscussionTypeEnum.Standard,
            DateTime.UtcNow, DateTime.UtcNow,
            false, false);

        // Assert
        await Assert.That(discussion.DomainEvents).IsEmpty();
    }

    [Test]
    public async Task RehydrateForList_WithNullLastActivityAt_WorksCorrectly()
    {
        // Arrange & Act
        var discussion = Discussion.RehydrateForList(
            DiscussionId.New(), SpaceId.New(), UserId.New(),
            "Test", "test", DiscussionTypeEnum.Standard,
            DateTime.UtcNow,
            lastActivityAt: null,
            isPinned: false, isLocked: false);

        // Assert
        await Assert.That(discussion.LastActivityAt).IsNull();
    }

    #endregion
}
