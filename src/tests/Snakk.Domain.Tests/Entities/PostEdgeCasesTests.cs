using FluentAssertions;
using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;

namespace Snakk.Domain.Tests.Entities;

/// <summary>
/// Additional edge case tests for Post entity focusing on boundary conditions
/// </summary>
public class PostEdgeCasesTests
{
    #region 5-Minute Hard Delete Boundary Tests

    [Fact]
    public void CanHardDelete_Exactly299Seconds_ReturnsTrue()
    {
        // Arrange - 4 minutes 59 seconds ago (just under 5 minutes)
        var createdAt = DateTime.UtcNow.AddSeconds(-299);
        var post = Post.Rehydrate(PostId.New(), DiscussionId.New(), UserId.New(), "content", createdAt);

        // Act
        var canHardDelete = post.CanHardDelete();

        // Assert
        canHardDelete.Should().BeTrue("post created 4:59 ago should allow hard delete");
    }

    [Fact]
    public void CanHardDelete_Exactly300Seconds_ReturnsFalse()
    {
        // Arrange - Exactly 5 minutes (300 seconds) ago
        var createdAt = DateTime.UtcNow.AddSeconds(-300);
        var post = Post.Rehydrate(PostId.New(), DiscussionId.New(), UserId.New(), "content", createdAt);

        // Act
        var canHardDelete = post.CanHardDelete();

        // Assert
        canHardDelete.Should().BeFalse("post created exactly 5:00 ago should not allow hard delete");
    }

    [Fact]
    public void CanHardDelete_Exactly301Seconds_ReturnsFalse()
    {
        // Arrange - 5 minutes 1 second ago (just over 5 minutes)
        var createdAt = DateTime.UtcNow.AddSeconds(-301);
        var post = Post.Rehydrate(PostId.New(), DiscussionId.New(), UserId.New(), "content", createdAt);

        // Act
        var canHardDelete = post.CanHardDelete();

        // Assert
        canHardDelete.Should().BeFalse("post created 5:01 ago should not allow hard delete");
    }

    [Fact]
    public void CanHardDelete_1SecondOld_ReturnsTrue()
    {
        // Arrange - Very recently created post
        var createdAt = DateTime.UtcNow.AddSeconds(-1);
        var post = Post.Rehydrate(PostId.New(), DiscussionId.New(), UserId.New(), "content", createdAt);

        // Act
        var canHardDelete = post.CanHardDelete();

        // Assert
        canHardDelete.Should().BeTrue("very recent post should allow hard delete");
    }

    [Fact]
    public void CanHardDelete_JustCreated_ReturnsTrue()
    {
        // Arrange - Post created right now
        var post = Post.Create(DiscussionId.New(), UserId.New(), "content");

        // Act
        var canHardDelete = post.CanHardDelete();

        // Assert
        canHardDelete.Should().BeTrue("newly created post should allow hard delete");
    }

    #endregion

    #region Content Edge Cases

    [Fact]
    public void Create_WithVeryLongContent_CreatesPost()
    {
        // Arrange - 10,000 character content
        var longContent = new string('a', 10000);

        // Act
        var post = Post.Create(DiscussionId.New(), UserId.New(), longContent);

        // Assert
        post.Content.Should().HaveLength(10000);
    }

    [Fact]
    public void UpdateContent_WithEmptyString_ThrowsArgumentException()
    {
        // Arrange
        var userId = UserId.New();
        var post = Post.Create(DiscussionId.New(), userId, "Original content");

        // Act
        var act = () => post.UpdateContent("", userId);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*content*");
    }

    [Fact]
    public void UpdateContent_WithWhitespaceOnly_ThrowsArgumentException()
    {
        // Arrange
        var userId = UserId.New();
        var post = Post.Create(DiscussionId.New(), userId, "Original content");

        // Act
        var act = () => post.UpdateContent("   ", userId);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*content*");
    }

    [Fact]
    public void UpdateContent_MultipleTimesInSuccession_CreatesMultipleRevisions()
    {
        // Arrange
        var userId = UserId.New();
        var post = Post.Create(DiscussionId.New(), userId, "Original");

        // Act
        post.UpdateContent("Edit 1", userId);
        post.UpdateContent("Edit 2", userId);
        post.UpdateContent("Edit 3", userId);

        // Assert
        post.Content.Should().Be("Edit 3");
        post.RevisionCount.Should().Be(3);
        post.Revisions.Should().HaveCount(3);
    }

    #endregion

    #region Permission Edge Cases

    [Fact]
    public void CanEdit_AfterSoftDelete_ReturnsTrue()
    {
        // Arrange
        var userId = UserId.New();
        var sixMinutesAgo = DateTime.UtcNow.AddMinutes(-6);
        var post = Post.Rehydrate(PostId.New(), DiscussionId.New(), userId, "content", sixMinutesAgo);
        post.SoftDelete(userId);

        // Act
        var canEdit = post.CanEdit(userId);

        // Assert
        canEdit.Should().BeTrue("CanEdit only checks authorship, not deletion status");
    }

    [Fact]
    public void CanEdit_ByAuthorBeforeDelete_ReturnsTrue()
    {
        // Arrange
        var userId = UserId.New();
        var post = Post.Create(DiscussionId.New(), userId, "content");

        // Act
        var canEdit = post.CanEdit(userId);

        // Assert
        canEdit.Should().BeTrue("author should be able to edit their own post");
    }

    [Fact]
    public void CanDelete_ByAuthor_ReturnsTrue()
    {
        // Arrange
        var userId = UserId.New();
        var post = Post.Create(DiscussionId.New(), userId, "content");

        // Act
        var canDelete = post.CanDelete(userId);

        // Assert
        canDelete.Should().BeTrue("author should be able to delete their own post");
    }

    [Fact]
    public void CanDelete_ByDifferentUser_ReturnsFalse()
    {
        // Arrange
        var authorId = UserId.New();
        var differentUserId = UserId.New();
        var post = Post.Create(DiscussionId.New(), authorId, "content");

        // Act
        var canDelete = post.CanDelete(differentUserId);

        // Assert
        canDelete.Should().BeFalse("non-authors cannot delete posts");
    }

    #endregion

    #region Revision Edge Cases

    [Fact]
    public void Revisions_AfterMultipleEdits_ContainsAllPreviousVersions()
    {
        // Arrange
        var userId = UserId.New();
        var post = Post.Create(DiscussionId.New(), userId, "Version 1");

        // Act
        post.UpdateContent("Version 2", userId);
        post.UpdateContent("Version 3", userId);

        // Assert
        post.Revisions.Should().HaveCount(2);
        post.Revisions.ToList()[0].Content.Should().Be("Version 1");
        post.Revisions.ToList()[1].Content.Should().Be("Version 2");
    }

    [Fact]
    public void UpdateContent_SameContent_StillCreatesRevision()
    {
        // Arrange
        var userId = UserId.New();
        var post = Post.Create(DiscussionId.New(), userId, "Same content");

        // Act
        post.UpdateContent("Same content", userId);

        // Assert
        post.RevisionCount.Should().Be(1);
        post.Revisions.Should().HaveCount(1);
    }

    #endregion

    #region Delete Edge Cases

    [Fact]
    public void SoftDelete_AlreadyDeleted_ThrowsInvalidOperationException()
    {
        // Arrange
        var userId = UserId.New();
        var sixMinutesAgo = DateTime.UtcNow.AddMinutes(-6);
        var post = Post.Rehydrate(PostId.New(), DiscussionId.New(), userId, "content", sixMinutesAgo);
        post.SoftDelete(userId);

        // Act
        var act = () => post.SoftDelete(userId);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Post is already deleted");
    }

    [Fact]
    public void HardDelete_WithinFiveMinutes_GeneratesEvent()
    {
        // Arrange
        var userId = UserId.New();
        var post = Post.Create(DiscussionId.New(), userId, "content");
        post.ClearDomainEvents(); // Clear creation event

        // Act
        post.HardDelete(userId);

        // Assert
        post.DomainEvents.Should().HaveCount(1);
    }

    [Fact]
    public void SoftDelete_AfterFiveMinutes_GeneratesEvent()
    {
        // Arrange
        var userId = UserId.New();
        var sixMinutesAgo = DateTime.UtcNow.AddMinutes(-6);
        var post = Post.Rehydrate(PostId.New(), DiscussionId.New(), userId, "content", sixMinutesAgo);

        // Act
        post.SoftDelete(userId);

        // Assert
        post.DomainEvents.Should().HaveCount(1);
    }

    #endregion

    #region Reply Edge Cases

    [Fact]
    public void Create_WithReplyToPostId_SetsReplyToPostId()
    {
        // Arrange
        var replyToPostId = PostId.New();

        // Act
        var post = Post.Create(DiscussionId.New(), UserId.New(), "Reply", replyToPostId: replyToPostId);

        // Assert
        post.ReplyToPostId.Should().Be(replyToPostId);
    }

    [Fact]
    public void Create_WithNullReplyToPostId_SetsReplyToPostIdToNull()
    {
        // Act
        var post = Post.Create(DiscussionId.New(), UserId.New(), "Post", replyToPostId: null);

        // Assert
        post.ReplyToPostId.Should().BeNull();
    }

    #endregion
}
