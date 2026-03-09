using Moq;
using Snakk.Application.Services;
using Snakk.Application.UseCases;
using Snakk.Domain;
using Snakk.Domain.Entities;
using Snakk.Domain.Events;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Models;

namespace Snakk.Application.Tests.Scenarios;

/// <summary>
/// Comprehensive workflow tests for post lifecycle scenarios
/// </summary>
public class PostLifecycleWorkflowTests
{
    private readonly Mock<IPostRepository> _mockPostRepository = new();
    private readonly Mock<IDiscussionRepository> _mockDiscussionRepository = new();
    private readonly Mock<IUserRepository> _mockUserRepository = new();
    private readonly Mock<IDomainEventDispatcher> _mockEventDispatcher = new();
    private readonly Mock<IRealtimeNotifier> _mockRealtimeNotifier = new();
    private readonly Mock<IFollowRepository> _mockFollowRepository = new();
    private readonly Mock<ICounterService> _mockCounterService = new();
    private readonly Mock<IMediaService> _mockMediaService = new();
    private Mock<ReactionUseCase> _mockReactionUseCase = null!;
    private PostUseCase _useCase = null!;

    [Before(Test)]
    public void Setup()
    {
        _mockReactionUseCase = new Mock<ReactionUseCase>(
            Mock.Of<IReactionRepository>(),
            Mock.Of<IPostRepository>(),
            Mock.Of<IRealtimeNotifier>(),
            Mock.Of<ICounterService>());

        _useCase = new PostUseCase(
            _mockPostRepository.Object,
            _mockDiscussionRepository.Object,
            _mockUserRepository.Object,
            _mockFollowRepository.Object,
            _mockEventDispatcher.Object,
            _mockRealtimeNotifier.Object,
            _mockCounterService.Object,
            _mockMediaService.Object,
            _mockReactionUseCase.Object);
    }

    [Test]
    public async Task CompletePostLifecycle_CreateEditDelete_WorksEndToEnd()
    {
        // Arrange
        var discussionId = DiscussionId.New();
        var userId = UserId.New();
        var discussion = Discussion.Create(SpaceId.New(), userId, "Test Discussion", "test-discussion");
        var user = User.CreateWithEmail("TestUser", "test@example.com", "hash", "token");

        _mockDiscussionRepository.Setup(r => r.GetByPublicIdAsync(discussionId))
            .ReturnsAsync(discussion);
        _mockUserRepository.Setup(r => r.GetByPublicIdAsync(userId))
            .ReturnsAsync(user);

        // Step 1: Create post
        var createResult = await _useCase.CreatePostAsync(discussionId, userId, "Original content");

        // Assert creation
        await Assert.That(createResult.IsSuccess).IsTrue();
        var post = createResult.Value!;
        await Assert.That(post.Content).IsEqualTo("Original content");
        await Assert.That(post.RevisionCount).IsEqualTo(0);
        await Assert.That(post.IsDeleted).IsFalse();

        _mockPostRepository.Verify(r => r.AddAsync(It.IsAny<Post>()), Times.Once);
        _mockCounterService.Verify(c => c.IncrementPostCountAsync(discussionId), Times.Once);

        // Step 2: Edit post
        _mockPostRepository.Setup(r => r.GetByPublicIdAsync(post.PublicId))
            .ReturnsAsync(post);

        var updateResult = await _useCase.UpdatePostAsync(post.PublicId, userId, "Updated content");

        // Assert update
        await Assert.That(updateResult.IsSuccess).IsTrue();
        await Assert.That(post.Content).IsEqualTo("Updated content");
        await Assert.That(post.RevisionCount).IsEqualTo(1);
        await Assert.That(post.EditedAt).IsNotNull();

        _mockPostRepository.Verify(r => r.UpdateAsync(post), Times.Once);
        _mockPostRepository.Verify(r => r.AddRevisionAsync(It.IsAny<PostRevision>()), Times.Once);

        // Step 3: Delete post (hard delete - within 5 minutes)
        var deleteResult = await _useCase.DeletePostAsync(post.PublicId, userId);

        // Assert deletion
        await Assert.That(deleteResult.IsSuccess).IsTrue();
        _mockPostRepository.Verify(r => r.DeleteAsync(post), Times.Once); // Hard delete
        _mockCounterService.Verify(c => c.DecrementPostCountAsync(discussionId), Times.Once);
        _mockRealtimeNotifier.Verify(n => n.NotifyPostDeletedAsync(post.PublicId, discussionId, true), Times.Once);
    }

    [Test]
    public async Task PostLifecycle_CreateEditMultipleTimes_MaintainsRevisionHistory()
    {
        // Arrange
        var discussionId = DiscussionId.New();
        var userId = UserId.New();
        var discussion = Discussion.Create(SpaceId.New(), userId, "Test Discussion", "test-discussion");
        var user = User.CreateWithEmail("TestUser", "test@example.com", "hash", "token");

        _mockDiscussionRepository.Setup(r => r.GetByPublicIdAsync(discussionId))
            .ReturnsAsync(discussion);
        _mockUserRepository.Setup(r => r.GetByPublicIdAsync(userId))
            .ReturnsAsync(user);

        // Step 1: Create
        var createResult = await _useCase.CreatePostAsync(discussionId, userId, "Version 1");
        var post = createResult.Value!;

        _mockPostRepository.Setup(r => r.GetByPublicIdAsync(post.PublicId))
            .ReturnsAsync(post);

        // Step 2: Edit multiple times
        await _useCase.UpdatePostAsync(post.PublicId, userId, "Version 2");
        await _useCase.UpdatePostAsync(post.PublicId, userId, "Version 3");
        await _useCase.UpdatePostAsync(post.PublicId, userId, "Version 4");

        // Assert
        await Assert.That(post.Content).IsEqualTo("Version 4");
        await Assert.That(post.RevisionCount).IsEqualTo(3);
        await Assert.That(post.Revisions).Count().IsEqualTo(3);
        await Assert.That(post.Revisions.ElementAt(0).Content).IsEqualTo("Version 1");
        await Assert.That(post.Revisions.ElementAt(1).Content).IsEqualTo("Version 2");
        await Assert.That(post.Revisions.ElementAt(2).Content).IsEqualTo("Version 3");

        // Only new revisions are saved (not all revisions on every update)
        // First edit: 1 revision saved, Second edit: 1 revision saved, Third edit: 1 revision saved = 3 total calls
        _mockPostRepository.Verify(r => r.AddRevisionAsync(It.IsAny<PostRevision>()), Times.Exactly(3));
    }

    [Test]
    public async Task PostLifecycle_CreateOldPostThenDelete_PerformsSoftDelete()
    {
        // Arrange - Create post that's already > 5 minutes old
        var discussionId = DiscussionId.New();
        var userId = UserId.New();
        var sixMinutesAgo = DateTime.UtcNow.AddMinutes(-6);
        var oldPost = Post.Rehydrate(PostId.New(), discussionId, userId, "Old content", sixMinutesAgo);

        _mockPostRepository.Setup(r => r.GetByPublicIdAsync(oldPost.PublicId))
            .ReturnsAsync(oldPost);

        // Act - Delete old post
        var deleteResult = await _useCase.DeletePostAsync(oldPost.PublicId, userId);

        // Assert - Should be soft delete
        await Assert.That(deleteResult.IsSuccess).IsTrue();
        await Assert.That(oldPost.IsDeleted).IsTrue();
        _mockPostRepository.Verify(r => r.UpdateAsync(oldPost), Times.Once); // Soft delete
        _mockPostRepository.Verify(r => r.DeleteAsync(It.IsAny<Post>()), Times.Never); // Not hard delete
        _mockRealtimeNotifier.Verify(n => n.NotifyPostDeletedAsync(oldPost.PublicId, discussionId, false), Times.Once);
    }

    [Test]
    public async Task PostLifecycle_CreateReplyChain_MaintainsReplyReferences()
    {
        // Arrange
        var discussionId = DiscussionId.New();
        var userId = UserId.New();
        var discussion = Discussion.Create(SpaceId.New(), userId, "Test Discussion", "test-discussion");
        var user = User.CreateWithEmail("TestUser", "test@example.com", "hash", "token");

        _mockDiscussionRepository.Setup(r => r.GetByPublicIdAsync(discussionId))
            .ReturnsAsync(discussion);
        _mockUserRepository.Setup(r => r.GetByPublicIdAsync(userId))
            .ReturnsAsync(user);

        // Step 1: Create original post
        var originalResult = await _useCase.CreatePostAsync(discussionId, userId, "Original post");
        var originalPost = originalResult.Value!;

        // Step 2: Create first reply
        _mockPostRepository.Setup(r => r.GetByPublicIdAsync(originalPost.PublicId))
            .ReturnsAsync(originalPost);

        var reply1Result = await _useCase.CreatePostAsync(discussionId, userId, "Reply 1", originalPost.PublicId);
        var reply1 = reply1Result.Value!;

        // Step 3: Create reply to reply
        _mockPostRepository.Setup(r => r.GetByPublicIdAsync(reply1.PublicId))
            .ReturnsAsync(reply1);

        var reply2Result = await _useCase.CreatePostAsync(discussionId, userId, "Reply 2", reply1.PublicId);
        var reply2 = reply2Result.Value!;

        // Assert reply chain
        await Assert.That((object?)originalPost.ReplyToPostId).IsNull();
        await Assert.That(reply1.ReplyToPostId!.Value).IsEqualTo(originalPost.PublicId.Value);
        await Assert.That(reply2.ReplyToPostId!.Value).IsEqualTo(reply1.PublicId.Value);

        _mockPostRepository.Verify(r => r.AddAsync(It.IsAny<Post>()), Times.Exactly(3));
    }

    [Test]
    public async Task PostLifecycle_CreateInLockedDiscussion_Fails()
    {
        // Arrange
        var discussionId = DiscussionId.New();
        var userId = UserId.New();
        var discussion = Discussion.Create(SpaceId.New(), userId, "Test Discussion", "test-discussion");
        discussion.Lock(); // Lock the discussion

        _mockDiscussionRepository.Setup(r => r.GetByPublicIdAsync(discussionId))
            .ReturnsAsync(discussion);

        // Act
        var createResult = await _useCase.CreatePostAsync(discussionId, userId, "Blocked content");

        // Assert
        await Assert.That(createResult.IsSuccess).IsFalse();
        await Assert.That(createResult.Error).Contains("locked");
        _mockPostRepository.Verify(r => r.AddAsync(It.IsAny<Post>()), Times.Never);
    }

    [Test]
    public async Task PostLifecycle_EditDeletedPost_Fails()
    {
        // Arrange
        var discussionId = DiscussionId.New();
        var userId = UserId.New();
        var sixMinutesAgo = DateTime.UtcNow.AddMinutes(-6);
        var post = Post.Rehydrate(PostId.New(), discussionId, userId, "Content", sixMinutesAgo);
        post.SoftDelete(userId); // Delete the post

        var user = User.CreateWithEmail("TestUser", "test@example.com", "hash", "token");
        var discussion = Discussion.Create(SpaceId.New(), userId, "Test Discussion", "test-discussion");

        _mockPostRepository.Setup(r => r.GetByPublicIdAsync(post.PublicId))
            .ReturnsAsync(post);
        _mockUserRepository.Setup(r => r.GetByPublicIdAsync(userId))
            .ReturnsAsync(user);
        _mockDiscussionRepository.Setup(r => r.GetByPublicIdAsync(discussionId))
            .ReturnsAsync(discussion);

        // Act
        var updateResult = await _useCase.UpdatePostAsync(post.PublicId, userId, "New content");

        // Assert
        await Assert.That(updateResult.IsSuccess).IsFalse();
        await Assert.That(updateResult.Error).Contains("deleted");
        _mockPostRepository.Verify(r => r.UpdateAsync(It.IsAny<Post>()), Times.Never);
    }

    [Test]
    public async Task PostLifecycle_NonAuthorEdit_Fails()
    {
        // Arrange
        var discussionId = DiscussionId.New();
        var authorId = UserId.New();
        var otherUserId = UserId.New();

        var post = Post.Create(discussionId, authorId, "Original content");
        var otherUser = User.CreateWithEmail("OtherUser", "other@example.com", "hash", "token");
        var discussion = Discussion.Create(SpaceId.New(), authorId, "Test Discussion", "test-discussion");

        _mockPostRepository.Setup(r => r.GetByPublicIdAsync(post.PublicId))
            .ReturnsAsync(post);
        _mockUserRepository.Setup(r => r.GetByPublicIdAsync(otherUserId))
            .ReturnsAsync(otherUser);
        _mockDiscussionRepository.Setup(r => r.GetByPublicIdAsync(discussionId))
            .ReturnsAsync(discussion);

        // Act
        var updateResult = await _useCase.UpdatePostAsync(post.PublicId, otherUserId, "Unauthorized edit");

        // Assert
        await Assert.That(updateResult.IsSuccess).IsFalse();
        _mockPostRepository.Verify(r => r.UpdateAsync(It.IsAny<Post>()), Times.Never);
    }

    [Test]
    public async Task PostLifecycle_GetPostHistory_ReturnsAllRevisions()
    {
        // Arrange
        var postId = PostId.New();
        var revisions = new List<PostRevision>
        {
            PostRevision.Create(postId, "Version 1", UserId.New(), 1),
            PostRevision.Create(postId, "Version 2", UserId.New(), 2),
            PostRevision.Create(postId, "Version 3", UserId.New(), 3)
        };

        _mockPostRepository.Setup(r => r.GetRevisionsAsync(postId))
            .ReturnsAsync(revisions);

        // Act
        var history = await _useCase.GetPostHistoryAsync(postId);

        // Assert
        await Assert.That(history).Count().IsEqualTo(3);
    }

    [Test]
    public async Task PostLifecycle_CreateEditWaitSixMinutesDelete_PerformsSoftDelete()
    {
        // This test simulates the real-world scenario where a post is created,
        // edited, and then deleted after the 5-minute window has passed

        // Arrange
        var discussionId = DiscussionId.New();
        var userId = UserId.New();

        // Create post 6 minutes ago
        var sixMinutesAgo = DateTime.UtcNow.AddMinutes(-6);
        var post = Post.Rehydrate(PostId.New(), discussionId, userId, "Original", sixMinutesAgo);

        // Edit it (still old)
        post.UpdateContent("Edited", userId);

        _mockPostRepository.Setup(r => r.GetByPublicIdAsync(post.PublicId))
            .ReturnsAsync(post);

        // Act - Try to delete
        var deleteResult = await _useCase.DeletePostAsync(post.PublicId, userId);

        // Assert - Should be soft delete because post is > 5 minutes old
        await Assert.That(deleteResult.IsSuccess).IsTrue();
        _mockPostRepository.Verify(r => r.UpdateAsync(post), Times.Once); // Soft delete
        _mockPostRepository.Verify(r => r.DeleteAsync(It.IsAny<Post>()), Times.Never);
        await Assert.That(post.IsDeleted).IsTrue();
    }
}
