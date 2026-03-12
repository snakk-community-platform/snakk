using Snakk.Domain.Entities;
using Snakk.Domain.Events;
using Snakk.Domain.ValueObjects;

namespace Snakk.Domain.Tests.Entities;

public class PostTests
{
    #region Create Tests

    [Test]
    public async Task Create_WithValidParameters_CreatesPost()
    {
        // Arrange
        var discussionId = DiscussionId.New();
        var userId = UserId.New();
        const string content = "This is a test post";

        // Act
        var post = Post.Create(discussionId, userId, content, "<p>This is a test post</p>");

        // Assert
        await Assert.That(post).IsNotNull();
        await Assert.That(post.PublicId).IsNotNull();
        await Assert.That(post.DiscussionId).IsEqualTo(discussionId);
        await Assert.That(post.CreatedByUserId).IsEqualTo(userId);
        await Assert.That(post.Content).IsEqualTo(content);
        await Assert.That(post.IsDeleted).IsFalse();
        await Assert.That(post.IsFirstPost).IsFalse();
        await Assert.That((object?)post.ReplyToPostId).IsNull();
        await Assert.That(post.EditedAt).IsNull();
        await Assert.That(post.RevisionCount).IsEqualTo(0);
        await Assert.That(post.CreatedAt).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task Create_FiresPostCreatedEvent()
    {
        // Arrange
        var discussionId = DiscussionId.New();
        var userId = UserId.New();

        // Act
        var post = Post.Create(discussionId, userId, "content", "<p>content</p>");

        // Assert
        await Assert.That(post.DomainEvents).Count().IsEqualTo(1);
        var domainEvent = post.DomainEvents.First();
        await Assert.That(domainEvent).IsTypeOf<PostCreatedEvent>();
        var postCreatedEvent = (PostCreatedEvent)domainEvent;
        await Assert.That(postCreatedEvent.PostId).IsEqualTo(post.PublicId);
        await Assert.That(postCreatedEvent.DiscussionId).IsEqualTo(discussionId);
        await Assert.That(postCreatedEvent.CreatedByUserId).IsEqualTo(userId);
    }

    [Test]
    public async Task Create_WithIsFirstPostTrue_SetsIsFirstPost()
    {
        // Arrange
        var discussionId = DiscussionId.New();
        var userId = UserId.New();

        // Act
        var post = Post.Create(discussionId, userId, "content", "<p>content</p>", isFirstPost: true);

        // Assert
        await Assert.That(post.IsFirstPost).IsTrue();
    }

    [Test]
    public async Task Create_WithReplyToPostId_SetsReplyToPostId()
    {
        // Arrange
        var discussionId = DiscussionId.New();
        var userId = UserId.New();
        var replyToPostId = PostId.New();

        // Act
        var post = Post.Create(discussionId, userId, "reply content", "<p>reply content</p>", replyToPostId: replyToPostId);

        // Assert
        await Assert.That(post.ReplyToPostId!).IsEqualTo(replyToPostId);
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task Create_WithEmptyContent_ThrowsArgumentException(string? emptyContent)
    {
        // Arrange
        var discussionId = DiscussionId.New();
        var userId = UserId.New();

        // Act & Assert
        await Assert.That(() => Post.Create(discussionId, userId, emptyContent!, "")).Throws<ArgumentException>();
    }

    #endregion

    #region UpdateContent Tests

    [Test]
    public async Task UpdateContent_WithValidContent_UpdatesContentAndCreatesRevision()
    {
        // Arrange
        var post = Post.Create(DiscussionId.New(), UserId.New(), "original content", "<p>original content</p>");
        var editorUserId = post.CreatedByUserId; // Same user
        post.ClearDomainEvents(); // Clear creation event

        // Act
        post.UpdateContent("updated content", "<p>updated content</p>", editorUserId);

        // Assert
        await Assert.That(post.Content).IsEqualTo("updated content");
        await Assert.That(post.EditedAt!.Value).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
        await Assert.That(post.LastModifiedAt!.Value).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
        await Assert.That(post.RevisionCount).IsEqualTo(1);
        await Assert.That(post.Revisions).Count().IsEqualTo(1);
        await Assert.That(post.Revisions.First().Content).IsEqualTo("original content");
    }

    [Test]
    public async Task UpdateContent_FiresPostEditedEvent()
    {
        // Arrange
        var discussionId = DiscussionId.New();
        var userId = UserId.New();
        var post = Post.Create(discussionId, userId, "original content", "<p>original content</p>");
        post.ClearDomainEvents();

        // Act
        post.UpdateContent("updated content", "<p>updated content</p>", userId);

        // Assert
        await Assert.That(post.DomainEvents).Count().IsEqualTo(1);
        var domainEvent = post.DomainEvents.First();
        await Assert.That(domainEvent).IsTypeOf<PostEditedEvent>();
        var postEditedEvent = (PostEditedEvent)domainEvent;
        await Assert.That(postEditedEvent.PostId).IsEqualTo(post.PublicId);
        await Assert.That(postEditedEvent.DiscussionId).IsEqualTo(discussionId);
    }

    [Test]
    public async Task UpdateContent_MultipleEdits_CreatesMultipleRevisions()
    {
        // Arrange
        var userId = UserId.New();
        var post = Post.Create(DiscussionId.New(), userId, "original", "<p>original</p>");

        // Act
        post.UpdateContent("edit1", "<p>edit1</p>", userId);
        post.UpdateContent("edit2", "<p>edit2</p>", userId);
        post.UpdateContent("edit3", "<p>edit3</p>", userId);

        // Assert
        await Assert.That(post.Content).IsEqualTo("edit3");
        await Assert.That(post.RevisionCount).IsEqualTo(3);
        await Assert.That(post.Revisions).Count().IsEqualTo(3);
        await Assert.That(post.Revisions.ToList()[0].Content).IsEqualTo("original");
        await Assert.That(post.Revisions.ToList()[1].Content).IsEqualTo("edit1");
        await Assert.That(post.Revisions.ToList()[2].Content).IsEqualTo("edit2");
    }

    [Test]
    public async Task UpdateContent_OnDeletedPost_ThrowsInvalidOperationException()
    {
        // Arrange
        var userId = UserId.New();
        var post = Post.Create(DiscussionId.New(), userId, "content", "<p>content</p>");
        post.SoftDelete(userId);

        // Act & Assert
        await Assert.That(() => post.UpdateContent("new content", "<p>new content</p>", userId)).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task UpdateContent_ByDifferentUser_ThrowsInvalidOperationException()
    {
        // Arrange
        var originalUserId = UserId.New();
        var differentUserId = UserId.New();
        var post = Post.Create(DiscussionId.New(), originalUserId, "content", "<p>content</p>");

        // Act & Assert
        await Assert.That(() => post.UpdateContent("new content", "<p>new content</p>", differentUserId)).Throws<InvalidOperationException>();
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task UpdateContent_WithEmptyContent_ThrowsArgumentException(string? emptyContent)
    {
        // Arrange
        var userId = UserId.New();
        var post = Post.Create(DiscussionId.New(), userId, "original", "<p>original</p>");

        // Act & Assert
        await Assert.That(() => post.UpdateContent(emptyContent!, "", userId)).Throws<ArgumentException>();
    }

    #endregion

    #region SoftDelete Tests

    [Test]
    public async Task SoftDelete_MarkPostAsDeleted()
    {
        // Arrange
        var userId = UserId.New();
        var discussionId = DiscussionId.New();
        var post = Post.Create(discussionId, userId, "content", "<p>content</p>");
        post.ClearDomainEvents();

        // Act
        post.SoftDelete(userId);

        // Assert
        await Assert.That(post.IsDeleted).IsTrue();
        await Assert.That(post.LastModifiedAt!.Value).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task SoftDelete_FiresPostDeletedEventWithSoftDeleteFlag()
    {
        // Arrange
        var userId = UserId.New();
        var discussionId = DiscussionId.New();
        var post = Post.Create(discussionId, userId, "content", "<p>content</p>");
        post.ClearDomainEvents();

        // Act
        post.SoftDelete(userId);

        // Assert
        await Assert.That(post.DomainEvents).Count().IsEqualTo(1);
        var domainEvent = post.DomainEvents.First();
        await Assert.That(domainEvent).IsTypeOf<PostDeletedEvent>();
        var deleteEvent = (PostDeletedEvent)domainEvent;
        await Assert.That(deleteEvent.PostId).IsEqualTo(post.PublicId);
        await Assert.That(deleteEvent.DiscussionId).IsEqualTo(discussionId);
        await Assert.That(deleteEvent.DeletedByUserId).IsEqualTo(userId);
        await Assert.That(deleteEvent.IsHardDelete).IsFalse();
    }

    [Test]
    public async Task SoftDelete_OnAlreadyDeletedPost_ThrowsInvalidOperationException()
    {
        // Arrange
        var userId = UserId.New();
        var post = Post.Create(DiscussionId.New(), userId, "content", "<p>content</p>");
        post.SoftDelete(userId);

        // Act & Assert
        await Assert.That(() => post.SoftDelete(userId)).Throws<InvalidOperationException>();
    }

    #endregion

    #region HardDelete Tests

    [Test]
    public async Task HardDelete_FiresPostDeletedEventWithHardDeleteFlag()
    {
        // Arrange
        var userId = UserId.New();
        var discussionId = DiscussionId.New();
        var post = Post.Create(discussionId, userId, "content", "<p>content</p>");
        post.ClearDomainEvents();

        // Act
        post.HardDelete(userId);

        // Assert
        await Assert.That(post.DomainEvents).Count().IsEqualTo(1);
        var domainEvent = post.DomainEvents.First();
        await Assert.That(domainEvent).IsTypeOf<PostDeletedEvent>();
        var deleteEvent = (PostDeletedEvent)domainEvent;
        await Assert.That(deleteEvent.IsHardDelete).IsTrue();
    }

    #endregion

    #region CanHardDelete Tests

    [Test]
    public async Task CanHardDelete_WithinFiveMinutes_ReturnsTrue()
    {
        // Arrange - Create post (will have current time)
        var post = Post.Create(DiscussionId.New(), UserId.New(), "content", "<p>content</p>");

        // Act
        var canHardDelete = post.CanHardDelete();

        // Assert
        await Assert.That(canHardDelete).IsTrue();
    }

    [Test]
    public async Task CanHardDelete_ExactlyFiveMinutesAgo_ReturnsFalse()
    {
        // Arrange - Rehydrate post with CreatedAt exactly 5 minutes ago
        var fiveMinutesAgo = DateTime.UtcNow.AddMinutes(-5);
        var post = Post.Rehydrate(
            PostId.New(),
            DiscussionId.New(),
            UserId.New(),
            "content",
            "<p>content</p>",
            fiveMinutesAgo);

        // Act
        var canHardDelete = post.CanHardDelete();

        // Assert
        await Assert.That(canHardDelete).IsFalse();
    }

    [Test]
    public async Task CanHardDelete_MoreThanFiveMinutesAgo_ReturnsFalse()
    {
        // Arrange - Rehydrate post with CreatedAt 10 minutes ago
        var tenMinutesAgo = DateTime.UtcNow.AddMinutes(-10);
        var post = Post.Rehydrate(
            PostId.New(),
            DiscussionId.New(),
            UserId.New(),
            "content",
            "<p>content</p>",
            tenMinutesAgo);

        // Act
        var canHardDelete = post.CanHardDelete();

        // Assert
        await Assert.That(canHardDelete).IsFalse();
    }

    [Test]
    public async Task CanHardDelete_JustUnderFiveMinutes_ReturnsTrue()
    {
        // Arrange - 4 minutes 59 seconds ago
        var almostFiveMinutesAgo = DateTime.UtcNow.AddMinutes(-4).AddSeconds(-59);
        var post = Post.Rehydrate(
            PostId.New(),
            DiscussionId.New(),
            UserId.New(),
            "content",
            "<p>content</p>",
            almostFiveMinutesAgo);

        // Act
        var canHardDelete = post.CanHardDelete();

        // Assert
        await Assert.That(canHardDelete).IsTrue();
    }

    [Test]
    public async Task CanHardDelete_OneHourAgo_ReturnsFalse()
    {
        // Arrange
        var oneHourAgo = DateTime.UtcNow.AddHours(-1);
        var post = Post.Rehydrate(
            PostId.New(),
            DiscussionId.New(),
            UserId.New(),
            "content",
            "<p>content</p>",
            oneHourAgo);

        // Act
        var canHardDelete = post.CanHardDelete();

        // Assert
        await Assert.That(canHardDelete).IsFalse();
    }

    #endregion

    #region CanEdit Tests

    [Test]
    public async Task CanEdit_ByAuthor_ReturnsTrue()
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
    public async Task CanEdit_ByDifferentUser_ReturnsFalse()
    {
        // Arrange
        var authorId = UserId.New();
        var otherId = UserId.New();
        var post = Post.Create(DiscussionId.New(), authorId, "content", "<p>content</p>");

        // Act
        var canEdit = post.CanEdit(otherId);

        // Assert
        await Assert.That(canEdit).IsFalse();
    }

    #endregion

    #region CanDelete Tests

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
        var otherId = UserId.New();
        var post = Post.Create(DiscussionId.New(), authorId, "content", "<p>content</p>");

        // Act
        var canDelete = post.CanDelete(otherId);

        // Assert
        await Assert.That(canDelete).IsFalse();
    }

    #endregion

    #region ClearDomainEvents Tests

    [Test]
    public async Task ClearDomainEvents_RemovesAllEvents()
    {
        // Arrange
        var post = Post.Create(DiscussionId.New(), UserId.New(), "content", "<p>content</p>");
        await Assert.That(post.DomainEvents).Count().IsEqualTo(1);

        // Act
        post.ClearDomainEvents();

        // Assert
        await Assert.That(post.DomainEvents).IsEmpty();
    }

    #endregion

    #region Rehydrate Tests

    [Test]
    public async Task Rehydrate_WithAllParameters_CreatesPostWithExactState()
    {
        // Arrange
        var postId = PostId.New();
        var discussionId = DiscussionId.New();
        var userId = UserId.New();
        var replyToPostId = PostId.New();
        var createdAt = DateTime.UtcNow.AddDays(-1);
        var lastModifiedAt = DateTime.UtcNow.AddHours(-2);
        var editedAt = DateTime.UtcNow.AddHours(-1);
        var revisions = new List<PostRevision>();

        // Act
        var post = Post.Rehydrate(
            postId,
            discussionId,
            userId,
            "content",
            "<p>content</p>",
            createdAt,
            lastModifiedAt,
            editedAt,
            isFirstPost: true,
            replyToPostId,
            isDeleted: true,
            hasCodeBlock: false,
            revisionCount: 3,
            revisions: revisions);

        // Assert
        await Assert.That(post.PublicId).IsEqualTo(postId);
        await Assert.That(post.DiscussionId).IsEqualTo(discussionId);
        await Assert.That(post.CreatedByUserId).IsEqualTo(userId);
        await Assert.That(post.Content).IsEqualTo("content");
        await Assert.That(post.CreatedAt).IsEqualTo(createdAt);
        await Assert.That(post.LastModifiedAt).IsEqualTo(lastModifiedAt);
        await Assert.That(post.EditedAt).IsEqualTo(editedAt);
        await Assert.That(post.IsFirstPost).IsTrue();
        await Assert.That(post.ReplyToPostId!).IsEqualTo(replyToPostId);
        await Assert.That(post.IsDeleted).IsTrue();
        await Assert.That(post.RevisionCount).IsEqualTo(3);
        await Assert.That(post.DomainEvents).IsEmpty(); // Rehydrated posts have no events
    }

    #endregion
}
