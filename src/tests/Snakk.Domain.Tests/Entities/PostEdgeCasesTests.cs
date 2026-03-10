using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;

namespace Snakk.Domain.Tests.Entities;

/// <summary>
/// Additional edge case tests for Post entity focusing on boundary conditions
/// </summary>
public class PostEdgeCasesTests
{
    #region 5-Minute Hard Delete Boundary Tests

    [Test]
    public async Task CanHardDelete_Exactly299Seconds_ReturnsTrue()
    {
        // Arrange - 4 minutes 59 seconds ago (just under 5 minutes)
        var createdAt = DateTime.UtcNow.AddSeconds(-299);
        var post = Post.Rehydrate(PostId.New(), DiscussionId.New(), UserId.New(), "content", "<p>content</p>", createdAt);

        // Act
        var canHardDelete = post.CanHardDelete();

        // Assert
        await Assert.That(canHardDelete).IsTrue();
    }

    [Test]
    public async Task CanHardDelete_Exactly300Seconds_ReturnsFalse()
    {
        // Arrange - Exactly 5 minutes (300 seconds) ago
        var createdAt = DateTime.UtcNow.AddSeconds(-300);
        var post = Post.Rehydrate(PostId.New(), DiscussionId.New(), UserId.New(), "content", "<p>content</p>", createdAt);

        // Act
        var canHardDelete = post.CanHardDelete();

        // Assert
        await Assert.That(canHardDelete).IsFalse();
    }

    [Test]
    public async Task CanHardDelete_Exactly301Seconds_ReturnsFalse()
    {
        // Arrange - 5 minutes 1 second ago (just over 5 minutes)
        var createdAt = DateTime.UtcNow.AddSeconds(-301);
        var post = Post.Rehydrate(PostId.New(), DiscussionId.New(), UserId.New(), "content", "<p>content</p>", createdAt);

        // Act
        var canHardDelete = post.CanHardDelete();

        // Assert
        await Assert.That(canHardDelete).IsFalse();
    }

    [Test]
    public async Task CanHardDelete_1SecondOld_ReturnsTrue()
    {
        // Arrange - Very recently created post
        var createdAt = DateTime.UtcNow.AddSeconds(-1);
        var post = Post.Rehydrate(PostId.New(), DiscussionId.New(), UserId.New(), "content", "<p>content</p>", createdAt);

        // Act
        var canHardDelete = post.CanHardDelete();

        // Assert
        await Assert.That(canHardDelete).IsTrue();
    }

    [Test]
    public async Task CanHardDelete_JustCreated_ReturnsTrue()
    {
        // Arrange - Post created right now
        var post = Post.Create(DiscussionId.New(), UserId.New(), "content", "<p>content</p>");

        // Act
        var canHardDelete = post.CanHardDelete();

        // Assert
        await Assert.That(canHardDelete).IsTrue();
    }

    #endregion

    #region Content Edge Cases

    [Test]
    public async Task Create_WithVeryLongContent_CreatesPost()
    {
        // Arrange - 10,000 character content
        var longContent = new string('a', 10000);

        // Act
        var post = Post.Create(DiscussionId.New(), UserId.New(), longContent, "<p>content</p>");

        // Assert
        await Assert.That(post.Content).Length().IsEqualTo(10000);
    }

    [Test]
    public async Task UpdateContent_WithEmptyString_ThrowsArgumentException()
    {
        // Arrange
        var userId = UserId.New();
        var post = Post.Create(DiscussionId.New(), userId, "Original content", "<p>Original content</p>");

        // Act & Assert
        await Assert.That(() => post.UpdateContent("", "", userId)).Throws<ArgumentException>();
    }

    [Test]
    public async Task UpdateContent_WithWhitespaceOnly_ThrowsArgumentException()
    {
        // Arrange
        var userId = UserId.New();
        var post = Post.Create(DiscussionId.New(), userId, "Original content", "<p>Original content</p>");

        // Act & Assert
        await Assert.That(() => post.UpdateContent("   ", "", userId)).Throws<ArgumentException>();
    }

    [Test]
    public async Task UpdateContent_MultipleTimesInSuccession_CreatesMultipleRevisions()
    {
        // Arrange
        var userId = UserId.New();
        var post = Post.Create(DiscussionId.New(), userId, "Original", "<p>Original</p>");

        // Act
        post.UpdateContent("Edit 1", "<p>Edit 1</p>", userId);
        post.UpdateContent("Edit 2", "<p>Edit 2</p>", userId);
        post.UpdateContent("Edit 3", "<p>Edit 3</p>", userId);

        // Assert
        await Assert.That(post.Content).IsEqualTo("Edit 3");
        await Assert.That(post.RevisionCount).IsEqualTo(3);
        await Assert.That(post.Revisions).Count().IsEqualTo(3);
    }

    #endregion

    #region Permission Edge Cases

    [Test]
    public async Task CanEdit_AfterSoftDelete_ReturnsTrue()
    {
        // Arrange
        var userId = UserId.New();
        var sixMinutesAgo = DateTime.UtcNow.AddMinutes(-6);
        var post = Post.Rehydrate(PostId.New(), DiscussionId.New(), userId, "content", "<p>content</p>", sixMinutesAgo);
        post.SoftDelete(userId);

        // Act
        var canEdit = post.CanEdit(userId);

        // Assert
        await Assert.That(canEdit).IsTrue();
    }

    [Test]
    public async Task CanEdit_ByAuthorBeforeDelete_ReturnsTrue()
    {
        // Arrange
        var userId = UserId.New();
        var post = Post.Create(DiscussionId.New(), userId, "content", "<p>content</p>");

        // Act
        var canEdit = post.CanEdit(userId);

        // Assert
        await Assert.That(canEdit).IsTrue();
    }

    [Test]
    public async Task CanDelete_ByAuthor_ReturnsTrue()
    {
        // Arrange
        var userId = UserId.New();
        var post = Post.Create(DiscussionId.New(), userId, "content", "<p>content</p>");

        // Act
        var canDelete = post.CanDelete(userId);

        // Assert
        await Assert.That(canDelete).IsTrue();
    }

    [Test]
    public async Task CanDelete_ByDifferentUser_ReturnsFalse()
    {
        // Arrange
        var authorId = UserId.New();
        var differentUserId = UserId.New();
        var post = Post.Create(DiscussionId.New(), authorId, "content", "<p>content</p>");

        // Act
        var canDelete = post.CanDelete(differentUserId);

        // Assert
        await Assert.That(canDelete).IsFalse();
    }

    #endregion

    #region Revision Edge Cases

    [Test]
    public async Task Revisions_AfterMultipleEdits_ContainsAllPreviousVersions()
    {
        // Arrange
        var userId = UserId.New();
        var post = Post.Create(DiscussionId.New(), userId, "Version 1", "<p>Version 1</p>");

        // Act
        post.UpdateContent("Version 2", "<p>Version 2</p>", userId);
        post.UpdateContent("Version 3", "<p>Version 3</p>", userId);

        // Assert
        await Assert.That(post.Revisions).Count().IsEqualTo(2);
        await Assert.That(post.Revisions.ToList()[0].Content).IsEqualTo("Version 1");
        await Assert.That(post.Revisions.ToList()[1].Content).IsEqualTo("Version 2");
    }

    [Test]
    public async Task UpdateContent_SameContent_StillCreatesRevision()
    {
        // Arrange
        var userId = UserId.New();
        var post = Post.Create(DiscussionId.New(), userId, "Same content", "<p>Same content</p>");

        // Act
        post.UpdateContent("Same content", "<p>Same content</p>", userId);

        // Assert
        await Assert.That(post.RevisionCount).IsEqualTo(1);
        await Assert.That(post.Revisions).Count().IsEqualTo(1);
    }

    #endregion

    #region Delete Edge Cases

    [Test]
    public async Task SoftDelete_AlreadyDeleted_ThrowsInvalidOperationException()
    {
        // Arrange
        var userId = UserId.New();
        var sixMinutesAgo = DateTime.UtcNow.AddMinutes(-6);
        var post = Post.Rehydrate(PostId.New(), DiscussionId.New(), userId, "content", "<p>content</p>", sixMinutesAgo);
        post.SoftDelete(userId);

        // Act & Assert
        await Assert.That(() => post.SoftDelete(userId)).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task HardDelete_WithinFiveMinutes_GeneratesEvent()
    {
        // Arrange
        var userId = UserId.New();
        var post = Post.Create(DiscussionId.New(), userId, "content", "<p>content</p>");
        post.ClearDomainEvents(); // Clear creation event

        // Act
        post.HardDelete(userId);

        // Assert
        await Assert.That(post.DomainEvents).Count().IsEqualTo(1);
    }

    [Test]
    public async Task SoftDelete_AfterFiveMinutes_GeneratesEvent()
    {
        // Arrange
        var userId = UserId.New();
        var sixMinutesAgo = DateTime.UtcNow.AddMinutes(-6);
        var post = Post.Rehydrate(PostId.New(), DiscussionId.New(), userId, "content", "<p>content</p>", sixMinutesAgo);

        // Act
        post.SoftDelete(userId);

        // Assert
        await Assert.That(post.DomainEvents).Count().IsEqualTo(1);
    }

    #endregion

    #region Reply Edge Cases

    [Test]
    public async Task Create_WithReplyToPostId_SetsReplyToPostId()
    {
        // Arrange
        var replyToPostId = PostId.New();

        // Act
        var post = Post.Create(DiscussionId.New(), UserId.New(), "Reply", "<p>Reply</p>", replyToPostId: replyToPostId);

        // Assert
        await Assert.That(post.ReplyToPostId!).IsEqualTo(replyToPostId);
    }

    [Test]
    public async Task Create_WithNullReplyToPostId_SetsReplyToPostIdToNull()
    {
        // Act
        var post = Post.Create(DiscussionId.New(), UserId.New(), "Post", "<p>Post</p>", replyToPostId: null);

        // Assert
        await Assert.That((object?)post.ReplyToPostId).IsNull();
    }

    #endregion
}
