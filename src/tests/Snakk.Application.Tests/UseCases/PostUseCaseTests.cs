using Moq;
using Snakk.Application.Services;
using Snakk.Application.UseCases;
using Snakk.Domain;
using Snakk.Domain.Entities;
using Snakk.Domain.Events;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Models;

namespace Snakk.Application.Tests.UseCases;

public class PostUseCaseTests
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
        // ReactionUseCase needs its own dependencies
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

    #region CreatePostAsync Tests

    [Test]
    public async Task CreatePostAsync_WithValidParameters_CreatesPost()
    {
        // Arrange
        var discussionId = DiscussionId.New();
        var userId = UserId.New();
        const string content = "Test post content";

        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Test Discussion", "test-discussion");
        var user = User.CreateWithEmail("TestUser", "test@example.com", "hash", "token");

        _mockDiscussionRepository.Setup(r => r.GetByPublicIdAsync(discussionId))
            .ReturnsAsync(discussion);
        _mockUserRepository.Setup(r => r.GetByPublicIdAsync(userId))
            .ReturnsAsync(user);

        // Act
        var result = await _useCase.CreatePostAsync(discussionId, userId, content);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsNotNull();
        await Assert.That(result.Value!.Content).IsEqualTo(content);
        await Assert.That(result.Value.DiscussionId).IsEqualTo(discussionId);
        await Assert.That(result.Value.CreatedByUserId).IsEqualTo(userId);

        _mockPostRepository.Verify(r => r.AddAsync(It.IsAny<Post>()), Times.Once);
        _mockDiscussionRepository.Verify(r => r.UpdateAsync(discussion), Times.Once);
        _mockCounterService.Verify(c => c.IncrementPostCountAsync(discussionId), Times.Once);
        _mockEventDispatcher.Verify(d => d.DispatchAsync(It.IsAny<IEnumerable<IDomainEvent>>()), Times.Once);
        _mockRealtimeNotifier.Verify(n => n.NotifyPostCreatedAsync(It.IsAny<Post>(), user, discussion), Times.Once);
    }

    [Test]
    public async Task CreatePostAsync_WithNonExistentDiscussion_ReturnsFailure()
    {
        // Arrange
        var discussionId = DiscussionId.New();
        var userId = UserId.New();

        _mockDiscussionRepository.Setup(r => r.GetByPublicIdAsync(discussionId))
            .ReturnsAsync((Discussion?)null);

        // Act
        var result = await _useCase.CreatePostAsync(discussionId, userId, "content");

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Discussion");
        await Assert.That(result.Error).Contains("not found");
        _mockPostRepository.Verify(r => r.AddAsync(It.IsAny<Post>()), Times.Never);
    }

    [Test]
    public async Task CreatePostAsync_WithLockedDiscussion_ReturnsFailure()
    {
        // Arrange
        var discussionId = DiscussionId.New();
        var userId = UserId.New();
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Test Discussion", "test-discussion");
        discussion.Lock(); // Lock the discussion

        _mockDiscussionRepository.Setup(r => r.GetByPublicIdAsync(discussionId))
            .ReturnsAsync(discussion);

        // Act
        var result = await _useCase.CreatePostAsync(discussionId, userId, "content");

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("locked");
        _mockPostRepository.Verify(r => r.AddAsync(It.IsAny<Post>()), Times.Never);
    }

    [Test]
    public async Task CreatePostAsync_WithNonExistentUser_ReturnsFailure()
    {
        // Arrange
        var discussionId = DiscussionId.New();
        var userId = UserId.New();
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Test Discussion", "test-discussion");

        _mockDiscussionRepository.Setup(r => r.GetByPublicIdAsync(discussionId))
            .ReturnsAsync(discussion);
        _mockUserRepository.Setup(r => r.GetByPublicIdAsync(userId))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _useCase.CreatePostAsync(discussionId, userId, "content");

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("User");
        await Assert.That(result.Error).Contains("not found");
        _mockPostRepository.Verify(r => r.AddAsync(It.IsAny<Post>()), Times.Never);
    }

    [Test]
    public async Task CreatePostAsync_WithReplyToPost_ValidatesReplyToPostExists()
    {
        // Arrange
        var discussionId = DiscussionId.New();
        var userId = UserId.New();
        var replyToPostId = PostId.New();

        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Test Discussion", "test-discussion");
        var user = User.CreateWithEmail("TestUser", "test@example.com", "hash", "token");

        _mockDiscussionRepository.Setup(r => r.GetByPublicIdAsync(discussionId))
            .ReturnsAsync(discussion);
        _mockUserRepository.Setup(r => r.GetByPublicIdAsync(userId))
            .ReturnsAsync(user);
        _mockPostRepository.Setup(r => r.GetByPublicIdAsync(replyToPostId))
            .ReturnsAsync((Post?)null); // Reply-to post doesn't exist

        // Act
        var result = await _useCase.CreatePostAsync(discussionId, userId, "content", replyToPostId);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Reply-to post");
        await Assert.That(result.Error).Contains("not found");
    }

    [Test]
    public async Task CreatePostAsync_WithValidReplyToPost_CreatesReply()
    {
        // Arrange
        var discussionId = DiscussionId.New();
        var userId = UserId.New();
        var replyToPostId = PostId.New();

        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Test Discussion", "test-discussion");
        var user = User.CreateWithEmail("TestUser", "test@example.com", "hash", "token");
        var replyToPost = Post.Create(discussionId, UserId.New(), "Original post");

        _mockDiscussionRepository.Setup(r => r.GetByPublicIdAsync(discussionId))
            .ReturnsAsync(discussion);
        _mockUserRepository.Setup(r => r.GetByPublicIdAsync(userId))
            .ReturnsAsync(user);
        _mockPostRepository.Setup(r => r.GetByPublicIdAsync(replyToPostId))
            .ReturnsAsync(replyToPost);

        // Act
        var result = await _useCase.CreatePostAsync(discussionId, userId, "Reply content", replyToPostId);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsNotNull();
        await Assert.That(result.Value!.ReplyToPostId!).IsEqualTo(replyToPostId);
    }

    #endregion

    #region UpdatePostAsync Tests

    [Test]
    public async Task UpdatePostAsync_WithValidParameters_UpdatesPost()
    {
        // Arrange
        var userId = UserId.New();
        var discussionId = DiscussionId.New();
        var post = Post.Create(discussionId, userId, "Original content");
        const string newContent = "Updated content";

        var user = User.CreateWithEmail("TestUser", "test@example.com", "hash", "token");
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Test Discussion", "test-discussion");

        _mockPostRepository.Setup(r => r.GetByPublicIdAsync(post.PublicId))
            .ReturnsAsync(post);
        _mockUserRepository.Setup(r => r.GetByPublicIdAsync(userId))
            .ReturnsAsync(user);
        _mockDiscussionRepository.Setup(r => r.GetByPublicIdAsync(discussionId))
            .ReturnsAsync(discussion);

        // Act
        var result = await _useCase.UpdatePostAsync(post.PublicId, userId, newContent);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsNotNull();
        await Assert.That(result.Value!.Content).IsEqualTo(newContent);

        _mockPostRepository.Verify(r => r.UpdateAsync(post), Times.Once);
        _mockPostRepository.Verify(r => r.AddRevisionAsync(It.IsAny<PostRevision>()), Times.Once);
        _mockEventDispatcher.Verify(d => d.DispatchAsync(It.IsAny<IEnumerable<IDomainEvent>>()), Times.Once);
        _mockRealtimeNotifier.Verify(n => n.NotifyPostEditedAsync(post, user, discussion), Times.Once);
    }

    [Test]
    public async Task UpdatePostAsync_WithNonExistentPost_ReturnsFailure()
    {
        // Arrange
        var postId = PostId.New();
        var userId = UserId.New();

        _mockPostRepository.Setup(r => r.GetByPublicIdAsync(postId))
            .ReturnsAsync((Post?)null);

        // Act
        var result = await _useCase.UpdatePostAsync(postId, userId, "new content");

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Post");
        await Assert.That(result.Error).Contains("not found");
    }

    [Test]
    public async Task UpdatePostAsync_ByNonAuthor_ReturnsFailure()
    {
        // Arrange
        var authorId = UserId.New();
        var differentUserId = UserId.New();
        var discussionId = DiscussionId.New();
        var post = Post.Create(discussionId, authorId, "Original content");

        var user = User.CreateWithEmail("DifferentUser", "different@example.com", "hash", "token");
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Test Discussion", "test-discussion");

        _mockPostRepository.Setup(r => r.GetByPublicIdAsync(post.PublicId))
            .ReturnsAsync(post);
        _mockUserRepository.Setup(r => r.GetByPublicIdAsync(differentUserId))
            .ReturnsAsync(user);
        _mockDiscussionRepository.Setup(r => r.GetByPublicIdAsync(discussionId))
            .ReturnsAsync(discussion);

        // Act
        var result = await _useCase.UpdatePostAsync(post.PublicId, differentUserId, "new content");

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        _mockPostRepository.Verify(r => r.UpdateAsync(It.IsAny<Post>()), Times.Never);
    }

    [Test]
    public async Task UpdatePostAsync_OnDeletedPost_ReturnsFailure()
    {
        // Arrange
        var userId = UserId.New();
        var discussionId = DiscussionId.New();
        var post = Post.Create(discussionId, userId, "Original content");
        post.SoftDelete(userId); // Delete the post

        var user = User.CreateWithEmail("TestUser", "test@example.com", "hash", "token");
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Test Discussion", "test-discussion");

        _mockPostRepository.Setup(r => r.GetByPublicIdAsync(post.PublicId))
            .ReturnsAsync(post);
        _mockUserRepository.Setup(r => r.GetByPublicIdAsync(userId))
            .ReturnsAsync(user);
        _mockDiscussionRepository.Setup(r => r.GetByPublicIdAsync(discussionId))
            .ReturnsAsync(discussion);

        // Act
        var result = await _useCase.UpdatePostAsync(post.PublicId, userId, "new content");

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("deleted");
    }

    #endregion

    #region DeletePostAsync Tests

    [Test]
    public async Task DeletePostAsync_WithinFiveMinutes_PerformsHardDelete()
    {
        // Arrange
        var userId = UserId.New();
        var discussionId = DiscussionId.New();
        var post = Post.Create(discussionId, userId, "Test content");

        _mockPostRepository.Setup(r => r.GetByPublicIdAsync(post.PublicId))
            .ReturnsAsync(post);

        // Act
        var result = await _useCase.DeletePostAsync(post.PublicId, userId);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        _mockPostRepository.Verify(r => r.DeleteAsync(post), Times.Once); // Hard delete
        _mockPostRepository.Verify(r => r.UpdateAsync(It.IsAny<Post>()), Times.Never);
        _mockCounterService.Verify(c => c.DecrementPostCountAsync(discussionId), Times.Once);
        _mockRealtimeNotifier.Verify(n => n.NotifyPostDeletedAsync(post.PublicId, discussionId, true), Times.Once);
    }

    [Test]
    public async Task DeletePostAsync_AfterFiveMinutes_PerformsSoftDelete()
    {
        // Arrange
        var userId = UserId.New();
        var discussionId = DiscussionId.New();
        var sixMinutesAgo = DateTime.UtcNow.AddMinutes(-6);
        var post = Post.Rehydrate(PostId.New(), discussionId, userId, "Test content", sixMinutesAgo);

        _mockPostRepository.Setup(r => r.GetByPublicIdAsync(post.PublicId))
            .ReturnsAsync(post);

        // Act
        var result = await _useCase.DeletePostAsync(post.PublicId, userId);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        _mockPostRepository.Verify(r => r.UpdateAsync(post), Times.Once); // Soft delete
        _mockPostRepository.Verify(r => r.DeleteAsync(It.IsAny<Post>()), Times.Never);
        _mockCounterService.Verify(c => c.DecrementPostCountAsync(discussionId), Times.Once);
        _mockRealtimeNotifier.Verify(n => n.NotifyPostDeletedAsync(post.PublicId, discussionId, false), Times.Once);
    }

    [Test]
    public async Task DeletePostAsync_ByNonAuthor_ReturnsFailure()
    {
        // Arrange
        var authorId = UserId.New();
        var differentUserId = UserId.New();
        var discussionId = DiscussionId.New();
        var post = Post.Create(discussionId, authorId, "Test content");

        _mockPostRepository.Setup(r => r.GetByPublicIdAsync(post.PublicId))
            .ReturnsAsync(post);

        // Act
        var result = await _useCase.DeletePostAsync(post.PublicId, differentUserId);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("only delete your own posts");
        _mockPostRepository.Verify(r => r.DeleteAsync(It.IsAny<Post>()), Times.Never);
        _mockPostRepository.Verify(r => r.UpdateAsync(It.IsAny<Post>()), Times.Never);
    }

    [Test]
    public async Task DeletePostAsync_WithNonExistentPost_ReturnsFailure()
    {
        // Arrange
        var postId = PostId.New();
        var userId = UserId.New();

        _mockPostRepository.Setup(r => r.GetByPublicIdAsync(postId))
            .ReturnsAsync((Post?)null);

        // Act
        var result = await _useCase.DeletePostAsync(postId, userId);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Post");
        await Assert.That(result.Error).Contains("not found");
    }

    #endregion

    #region GetPostAsync Tests

    [Test]
    public async Task GetPostAsync_WithExistingPost_ReturnsPost()
    {
        // Arrange
        var post = Post.Create(DiscussionId.New(), UserId.New(), "Test content");

        _mockPostRepository.Setup(r => r.GetByPublicIdAsync(post.PublicId))
            .ReturnsAsync(post);

        // Act
        var result = await _useCase.GetPostAsync(post.PublicId);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsEqualTo(post);
    }

    [Test]
    public async Task GetPostAsync_WithNonExistentPost_ReturnsFailure()
    {
        // Arrange
        var postId = PostId.New();

        _mockPostRepository.Setup(r => r.GetByPublicIdAsync(postId))
            .ReturnsAsync((Post?)null);

        // Act
        var result = await _useCase.GetPostAsync(postId);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Post");
        await Assert.That(result.Error).Contains("not found");
    }

    #endregion

    #region GetPostsByDiscussionAsync Tests

    [Test]
    public async Task GetPostsByDiscussionAsync_ReturnsPagedResults()
    {
        // Arrange
        var discussionId = DiscussionId.New();
        var posts = new List<Post>
        {
            Post.Create(discussionId, UserId.New(), "Post 1"),
            Post.Create(discussionId, UserId.New(), "Post 2")
        };

        var pagedResult = new PagedResult<Post> { Items = posts, Offset = 0, PageSize = 20, HasMoreItems = false };

        _mockPostRepository.Setup(r => r.GetPagedByDiscussionIdAsync(discussionId, 0, 20))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _useCase.GetPostsByDiscussionAsync(discussionId, 0, 20);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Items).Count().IsEqualTo(2);
        await Assert.That(result.Offset).IsEqualTo(0);
        await Assert.That(result.PageSize).IsEqualTo(20);
        await Assert.That(result.HasMoreItems).IsFalse();
    }

    #endregion

    #region GetPostHistoryAsync Tests

    [Test]
    public async Task GetPostHistoryAsync_ReturnsRevisions()
    {
        // Arrange
        var postId = PostId.New();
        var revisions = new List<PostRevision>
        {
            PostRevision.Create(postId, "Old content 1", UserId.New(), 1),
            PostRevision.Create(postId, "Old content 2", UserId.New(), 2)
        };

        _mockPostRepository.Setup(r => r.GetRevisionsAsync(postId))
            .ReturnsAsync(revisions);

        // Act
        var result = await _useCase.GetPostHistoryAsync(postId);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result).Count().IsEqualTo(2);
    }

    #endregion

    #region Edge Cases

    [Test]
    public async Task CreatePostAsync_UpdatesDiscussionActivity()
    {
        // Arrange
        var discussionId = DiscussionId.New();
        var userId = UserId.New();
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Test Discussion", "test-discussion");
        var user = User.CreateWithEmail("TestUser", "test@example.com", "hash", "token");
        var originalActivity = discussion.LastActivityAt;

        Thread.Sleep(10); // Small delay

        _mockDiscussionRepository.Setup(r => r.GetByPublicIdAsync(discussionId))
            .ReturnsAsync(discussion);
        _mockUserRepository.Setup(r => r.GetByPublicIdAsync(userId))
            .ReturnsAsync(user);

        // Act
        await _useCase.CreatePostAsync(discussionId, userId, "content");

        // Assert
        await Assert.That(discussion.LastActivityAt!.Value).IsGreaterThan(originalActivity ?? DateTime.MinValue);
    }

    [Test]
    public async Task DeletePostAsync_ExactlyFiveMinutesAgo_PerformsSoftDelete()
    {
        // Arrange - Post created exactly 5 minutes ago
        var userId = UserId.New();
        var discussionId = DiscussionId.New();
        var fiveMinutesAgo = DateTime.UtcNow.AddMinutes(-5);
        var post = Post.Rehydrate(PostId.New(), discussionId, userId, "Test content", fiveMinutesAgo);

        _mockPostRepository.Setup(r => r.GetByPublicIdAsync(post.PublicId))
            .ReturnsAsync(post);

        // Act
        var result = await _useCase.DeletePostAsync(post.PublicId, userId);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        _mockPostRepository.Verify(r => r.UpdateAsync(post), Times.Once); // Soft delete
        _mockPostRepository.Verify(r => r.DeleteAsync(It.IsAny<Post>()), Times.Never);
    }

    #endregion
}
