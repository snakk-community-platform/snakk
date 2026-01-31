using FluentAssertions;
using Snakk.Domain.Entities;
using Snakk.Domain.Events;
using Snakk.Domain.ValueObjects;
using Xunit;

namespace Snakk.Domain.Tests.Entities;

public class PostTests
{
    #region Create Tests

    [Fact]
    public void Create_WithValidParameters_CreatesPost()
    {
        // Arrange
        var discussionId = DiscussionId.New();
        var userId = UserId.New();
        const string content = "This is a test post";

        // Act
        var post = Post.Create(discussionId, userId, content);

        // Assert
        post.Should().NotBeNull();
        post.PublicId.Should().NotBeNull();
        post.DiscussionId.Should().Be(discussionId);
        post.CreatedByUserId.Should().Be(userId);
        post.Content.Should().Be(content);
        post.IsDeleted.Should().BeFalse();
        post.IsFirstPost.Should().BeFalse();
        post.ReplyToPostId.Should().BeNull();
        post.EditedAt.Should().BeNull();
        post.RevisionCount.Should().Be(0);
        post.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Create_FiresPostCreatedEvent()
    {
        // Arrange
        var discussionId = DiscussionId.New();
        var userId = UserId.New();

        // Act
        var post = Post.Create(discussionId, userId, "content");

        // Assert
        post.DomainEvents.Should().HaveCount(1);
        var domainEvent = post.DomainEvents.First();
        domainEvent.Should().BeOfType<PostCreatedEvent>();
        var postCreatedEvent = (PostCreatedEvent)domainEvent;
        postCreatedEvent.PostId.Should().Be(post.PublicId);
        postCreatedEvent.DiscussionId.Should().Be(discussionId);
        postCreatedEvent.CreatedByUserId.Should().Be(userId);
    }

    [Fact]
    public void Create_WithIsFirstPostTrue_SetsIsFirstPost()
    {
        // Arrange
        var discussionId = DiscussionId.New();
        var userId = UserId.New();

        // Act
        var post = Post.Create(discussionId, userId, "content", isFirstPost: true);

        // Assert
        post.IsFirstPost.Should().BeTrue();
    }

    [Fact]
    public void Create_WithReplyToPostId_SetsReplyToPostId()
    {
        // Arrange
        var discussionId = DiscussionId.New();
        var userId = UserId.New();
        var replyToPostId = PostId.New();

        // Act
        var post = Post.Create(discussionId, userId, "reply content", replyToPostId: replyToPostId);

        // Assert
        post.ReplyToPostId.Should().Be(replyToPostId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyContent_ThrowsArgumentException(string? emptyContent)
    {
        // Arrange
        var discussionId = DiscussionId.New();
        var userId = UserId.New();

        // Act
        Action act = () => Post.Create(discussionId, userId, emptyContent!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Post content cannot be empty*");
    }

    #endregion

    #region UpdateContent Tests

    [Fact]
    public void UpdateContent_WithValidContent_UpdatesContentAndCreatesRevision()
    {
        // Arrange
        var post = Post.Create(DiscussionId.New(), UserId.New(), "original content");
        var editorUserId = post.CreatedByUserId; // Same user
        post.ClearDomainEvents(); // Clear creation event

        // Act
        post.UpdateContent("updated content", editorUserId);

        // Assert
        post.Content.Should().Be("updated content");
        post.EditedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        post.LastModifiedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        post.RevisionCount.Should().Be(1);
        post.Revisions.Should().HaveCount(1);
        post.Revisions.First().Content.Should().Be("original content");
    }

    [Fact]
    public void UpdateContent_FiresPostEditedEvent()
    {
        // Arrange
        var discussionId = DiscussionId.New();
        var userId = UserId.New();
        var post = Post.Create(discussionId, userId, "original content");
        post.ClearDomainEvents();

        // Act
        post.UpdateContent("updated content", userId);

        // Assert
        post.DomainEvents.Should().HaveCount(1);
        var domainEvent = post.DomainEvents.First();
        domainEvent.Should().BeOfType<PostEditedEvent>();
        var postEditedEvent = (PostEditedEvent)domainEvent;
        postEditedEvent.PostId.Should().Be(post.PublicId);
        postEditedEvent.DiscussionId.Should().Be(discussionId);
    }

    [Fact]
    public void UpdateContent_MultipleEdits_CreatesMultipleRevisions()
    {
        // Arrange
        var userId = UserId.New();
        var post = Post.Create(DiscussionId.New(), userId, "original");

        // Act
        post.UpdateContent("edit1", userId);
        post.UpdateContent("edit2", userId);
        post.UpdateContent("edit3", userId);

        // Assert
        post.Content.Should().Be("edit3");
        post.RevisionCount.Should().Be(3);
        post.Revisions.Should().HaveCount(3);
        post.Revisions.ToList()[0].Content.Should().Be("original");
        post.Revisions.ToList()[1].Content.Should().Be("edit1");
        post.Revisions.ToList()[2].Content.Should().Be("edit2");
    }

    [Fact]
    public void UpdateContent_OnDeletedPost_ThrowsInvalidOperationException()
    {
        // Arrange
        var userId = UserId.New();
        var post = Post.Create(DiscussionId.New(), userId, "content");
        post.SoftDelete(userId);

        // Act
        Action act = () => post.UpdateContent("new content", userId);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Cannot edit a deleted post");
    }

    [Fact]
    public void UpdateContent_ByDifferentUser_ThrowsInvalidOperationException()
    {
        // Arrange
        var originalUserId = UserId.New();
        var differentUserId = UserId.New();
        var post = Post.Create(DiscussionId.New(), originalUserId, "content");

        // Act
        Action act = () => post.UpdateContent("new content", differentUserId);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Only the author can edit this post");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateContent_WithEmptyContent_ThrowsArgumentException(string? emptyContent)
    {
        // Arrange
        var userId = UserId.New();
        var post = Post.Create(DiscussionId.New(), userId, "original");

        // Act
        Action act = () => post.UpdateContent(emptyContent!, userId);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Post content cannot be empty*");
    }

    #endregion

    #region SoftDelete Tests

    [Fact]
    public void SoftDelete_MarkPostAsDeleted()
    {
        // Arrange
        var userId = UserId.New();
        var discussionId = DiscussionId.New();
        var post = Post.Create(discussionId, userId, "content");
        post.ClearDomainEvents();

        // Act
        post.SoftDelete(userId);

        // Assert
        post.IsDeleted.Should().BeTrue();
        post.LastModifiedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void SoftDelete_FiresPostDeletedEventWithSoftDeleteFlag()
    {
        // Arrange
        var userId = UserId.New();
        var discussionId = DiscussionId.New();
        var post = Post.Create(discussionId, userId, "content");
        post.ClearDomainEvents();

        // Act
        post.SoftDelete(userId);

        // Assert
        post.DomainEvents.Should().HaveCount(1);
        var domainEvent = post.DomainEvents.First();
        domainEvent.Should().BeOfType<PostDeletedEvent>();
        var deleteEvent = (PostDeletedEvent)domainEvent;
        deleteEvent.PostId.Should().Be(post.PublicId);
        deleteEvent.DiscussionId.Should().Be(discussionId);
        deleteEvent.DeletedByUserId.Should().Be(userId);
        deleteEvent.IsHardDelete.Should().BeFalse();
    }

    [Fact]
    public void SoftDelete_OnAlreadyDeletedPost_ThrowsInvalidOperationException()
    {
        // Arrange
        var userId = UserId.New();
        var post = Post.Create(DiscussionId.New(), userId, "content");
        post.SoftDelete(userId);

        // Act
        Action act = () => post.SoftDelete(userId);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Post is already deleted");
    }

    #endregion

    #region HardDelete Tests

    [Fact]
    public void HardDelete_FiresPostDeletedEventWithHardDeleteFlag()
    {
        // Arrange
        var userId = UserId.New();
        var discussionId = DiscussionId.New();
        var post = Post.Create(discussionId, userId, "content");
        post.ClearDomainEvents();

        // Act
        post.HardDelete(userId);

        // Assert
        post.DomainEvents.Should().HaveCount(1);
        var domainEvent = post.DomainEvents.First();
        domainEvent.Should().BeOfType<PostDeletedEvent>();
        var deleteEvent = (PostDeletedEvent)domainEvent;
        deleteEvent.IsHardDelete.Should().BeTrue();
    }

    #endregion

    #region CanHardDelete Tests

    [Fact]
    public void CanHardDelete_WithinFiveMinutes_ReturnsTrue()
    {
        // Arrange - Create post (will have current time)
        var post = Post.Create(DiscussionId.New(), UserId.New(), "content");

        // Act
        var canHardDelete = post.CanHardDelete();

        // Assert
        canHardDelete.Should().BeTrue();
    }

    [Fact]
    public void CanHardDelete_ExactlyFiveMinutesAgo_ReturnsFalse()
    {
        // Arrange - Rehydrate post with CreatedAt exactly 5 minutes ago
        var fiveMinutesAgo = DateTime.UtcNow.AddMinutes(-5);
        var post = Post.Rehydrate(
            PostId.New(),
            DiscussionId.New(),
            UserId.New(),
            "content",
            fiveMinutesAgo);

        // Act
        var canHardDelete = post.CanHardDelete();

        // Assert
        canHardDelete.Should().BeFalse();
    }

    [Fact]
    public void CanHardDelete_MoreThanFiveMinutesAgo_ReturnsFalse()
    {
        // Arrange - Rehydrate post with CreatedAt 10 minutes ago
        var tenMinutesAgo = DateTime.UtcNow.AddMinutes(-10);
        var post = Post.Rehydrate(
            PostId.New(),
            DiscussionId.New(),
            UserId.New(),
            "content",
            tenMinutesAgo);

        // Act
        var canHardDelete = post.CanHardDelete();

        // Assert
        canHardDelete.Should().BeFalse();
    }

    [Fact]
    public void CanHardDelete_JustUnderFiveMinutes_ReturnsTrue()
    {
        // Arrange - 4 minutes 59 seconds ago
        var almostFiveMinutesAgo = DateTime.UtcNow.AddMinutes(-4).AddSeconds(-59);
        var post = Post.Rehydrate(
            PostId.New(),
            DiscussionId.New(),
            UserId.New(),
            "content",
            almostFiveMinutesAgo);

        // Act
        var canHardDelete = post.CanHardDelete();

        // Assert
        canHardDelete.Should().BeTrue();
    }

    [Fact]
    public void CanHardDelete_OneHourAgo_ReturnsFalse()
    {
        // Arrange
        var oneHourAgo = DateTime.UtcNow.AddHours(-1);
        var post = Post.Rehydrate(
            PostId.New(),
            DiscussionId.New(),
            UserId.New(),
            "content",
            oneHourAgo);

        // Act
        var canHardDelete = post.CanHardDelete();

        // Assert
        canHardDelete.Should().BeFalse();
    }

    #endregion

    #region CanEdit Tests

    [Fact]
    public void CanEdit_ByAuthor_ReturnsTrue()
    {
        // Arrange
        var userId = UserId.New();
        var post = Post.Create(DiscussionId.New(), userId, "content");

        // Act
        var canEdit = post.CanEdit(userId);

        // Assert
        canEdit.Should().BeTrue();
    }

    [Fact]
    public void CanEdit_ByDifferentUser_ReturnsFalse()
    {
        // Arrange
        var authorId = UserId.New();
        var otherId = UserId.New();
        var post = Post.Create(DiscussionId.New(), authorId, "content");

        // Act
        var canEdit = post.CanEdit(otherId);

        // Assert
        canEdit.Should().BeFalse();
    }

    #endregion

    #region CanDelete Tests

    [Fact]
    public void CanDelete_ByAuthor_ReturnsTrue()
    {
        // Arrange
        var userId = UserId.New();
        var post = Post.Create(DiscussionId.New(), userId, "content");

        // Act
        var canDelete = post.CanDelete(userId);

        // Assert
        canDelete.Should().BeTrue();
    }

    [Fact]
    public void CanDelete_ByDifferentUser_ReturnsFalse()
    {
        // Arrange
        var authorId = UserId.New();
        var otherId = UserId.New();
        var post = Post.Create(DiscussionId.New(), authorId, "content");

        // Act
        var canDelete = post.CanDelete(otherId);

        // Assert
        canDelete.Should().BeFalse();
    }

    #endregion

    #region ClearDomainEvents Tests

    [Fact]
    public void ClearDomainEvents_RemovesAllEvents()
    {
        // Arrange
        var post = Post.Create(DiscussionId.New(), UserId.New(), "content");
        post.DomainEvents.Should().HaveCount(1);

        // Act
        post.ClearDomainEvents();

        // Assert
        post.DomainEvents.Should().BeEmpty();
    }

    #endregion

    #region Rehydrate Tests

    [Fact]
    public void Rehydrate_WithAllParameters_CreatesPostWithExactState()
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
            createdAt,
            lastModifiedAt,
            editedAt,
            isFirstPost: true,
            replyToPostId,
            isDeleted: true,
            revisionCount: 3,
            revisions);

        // Assert
        post.PublicId.Should().Be(postId);
        post.DiscussionId.Should().Be(discussionId);
        post.CreatedByUserId.Should().Be(userId);
        post.Content.Should().Be("content");
        post.CreatedAt.Should().Be(createdAt);
        post.LastModifiedAt.Should().Be(lastModifiedAt);
        post.EditedAt.Should().Be(editedAt);
        post.IsFirstPost.Should().BeTrue();
        post.ReplyToPostId.Should().Be(replyToPostId);
        post.IsDeleted.Should().BeTrue();
        post.RevisionCount.Should().Be(3);
        post.Revisions.Should().BeEquivalentTo(revisions);
        post.DomainEvents.Should().BeEmpty(); // Rehydrated posts have no events
    }

    #endregion
}
