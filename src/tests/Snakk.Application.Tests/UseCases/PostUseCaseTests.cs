using FluentAssertions;
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
    private readonly Mock<IPostRepository> _mockPostRepository;
    private readonly Mock<IDiscussionRepository> _mockDiscussionRepository;
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly Mock<IDomainEventDispatcher> _mockEventDispatcher;
    private readonly Mock<IRealtimeNotifier> _mockRealtimeNotifier;
    private readonly Mock<ICounterService> _mockCounterService;
    private readonly Mock<ReactionUseCase> _mockReactionUseCase;
    private readonly PostUseCase _useCase;

    public PostUseCaseTests()
    {
        _mockPostRepository = new Mock<IPostRepository>();
        _mockDiscussionRepository = new Mock<IDiscussionRepository>();
        _mockUserRepository = new Mock<IUserRepository>();
        _mockEventDispatcher = new Mock<IDomainEventDispatcher>();
        _mockRealtimeNotifier = new Mock<IRealtimeNotifier>();
        _mockCounterService = new Mock<ICounterService>();

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
            _mockEventDispatcher.Object,
            _mockRealtimeNotifier.Object,
            _mockCounterService.Object,
            _mockReactionUseCase.Object);
    }

    #region CreatePostAsync Tests

    [Fact]
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
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Content.Should().Be(content);
        result.Value.DiscussionId.Should().Be(discussionId);
        result.Value.CreatedByUserId.Should().Be(userId);

        _mockPostRepository.Verify(r => r.AddAsync(It.IsAny<Post>()), Times.Once);
        _mockDiscussionRepository.Verify(r => r.UpdateAsync(discussion), Times.Once);
        _mockCounterService.Verify(c => c.IncrementPostCountAsync(discussionId), Times.Once);
        _mockEventDispatcher.Verify(d => d.DispatchAsync(It.IsAny<IEnumerable<IDomainEvent>>()), Times.Once);
        _mockRealtimeNotifier.Verify(n => n.NotifyPostCreatedAsync(It.IsAny<Post>(), user, discussion), Times.Once);
    }

    [Fact]
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
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Discussion");
        result.Error.Should().Contain("not found");
        _mockPostRepository.Verify(r => r.AddAsync(It.IsAny<Post>()), Times.Never);
    }

    [Fact]
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
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("locked");
        _mockPostRepository.Verify(r => r.AddAsync(It.IsAny<Post>()), Times.Never);
    }

    [Fact]
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
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("User");
        result.Error.Should().Contain("not found");
        _mockPostRepository.Verify(r => r.AddAsync(It.IsAny<Post>()), Times.Never);
    }

    [Fact]
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
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Reply-to post");
        result.Error.Should().Contain("not found");
    }

    [Fact]
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
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.ReplyToPostId.Should().Be(replyToPostId);
    }

    #endregion

    #region UpdatePostAsync Tests

    [Fact]
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
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Content.Should().Be(newContent);

        _mockPostRepository.Verify(r => r.UpdateAsync(post), Times.Once);
        _mockPostRepository.Verify(r => r.AddRevisionAsync(It.IsAny<PostRevision>()), Times.Once);
        _mockEventDispatcher.Verify(d => d.DispatchAsync(It.IsAny<IEnumerable<IDomainEvent>>()), Times.Once);
        _mockRealtimeNotifier.Verify(n => n.NotifyPostEditedAsync(post, user, discussion), Times.Once);
    }

    [Fact]
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
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Post");
        result.Error.Should().Contain("not found");
    }

    [Fact]
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
        result.IsSuccess.Should().BeFalse();
        _mockPostRepository.Verify(r => r.UpdateAsync(It.IsAny<Post>()), Times.Never);
    }

    [Fact]
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
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("deleted");
    }

    #endregion

    #region DeletePostAsync Tests

    [Fact]
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
        result.IsSuccess.Should().BeTrue();
        _mockPostRepository.Verify(r => r.DeleteAsync(post), Times.Once); // Hard delete
        _mockPostRepository.Verify(r => r.UpdateAsync(It.IsAny<Post>()), Times.Never);
        _mockCounterService.Verify(c => c.DecrementPostCountAsync(discussionId), Times.Once);
        _mockRealtimeNotifier.Verify(n => n.NotifyPostDeletedAsync(post.PublicId, discussionId, true), Times.Once);
    }

    [Fact]
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
        result.IsSuccess.Should().BeTrue();
        _mockPostRepository.Verify(r => r.UpdateAsync(post), Times.Once); // Soft delete
        _mockPostRepository.Verify(r => r.DeleteAsync(It.IsAny<Post>()), Times.Never);
        _mockCounterService.Verify(c => c.DecrementPostCountAsync(discussionId), Times.Once);
        _mockRealtimeNotifier.Verify(n => n.NotifyPostDeletedAsync(post.PublicId, discussionId, false), Times.Once);
    }

    [Fact]
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
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("only delete your own posts");
        _mockPostRepository.Verify(r => r.DeleteAsync(It.IsAny<Post>()), Times.Never);
        _mockPostRepository.Verify(r => r.UpdateAsync(It.IsAny<Post>()), Times.Never);
    }

    [Fact]
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
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Post");
        result.Error.Should().Contain("not found");
    }

    #endregion

    #region GetPostAsync Tests

    [Fact]
    public async Task GetPostAsync_WithExistingPost_ReturnsPost()
    {
        // Arrange
        var post = Post.Create(DiscussionId.New(), UserId.New(), "Test content");

        _mockPostRepository.Setup(r => r.GetByPublicIdAsync(post.PublicId))
            .ReturnsAsync(post);

        // Act
        var result = await _useCase.GetPostAsync(post.PublicId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(post);
    }

    [Fact]
    public async Task GetPostAsync_WithNonExistentPost_ReturnsFailure()
    {
        // Arrange
        var postId = PostId.New();

        _mockPostRepository.Setup(r => r.GetByPublicIdAsync(postId))
            .ReturnsAsync((Post?)null);

        // Act
        var result = await _useCase.GetPostAsync(postId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Post");
        result.Error.Should().Contain("not found");
    }

    #endregion

    #region GetPostsByDiscussionAsync Tests

    [Fact]
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
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(2);
        result.Offset.Should().Be(0);
        result.PageSize.Should().Be(20);
        result.HasMoreItems.Should().BeFalse();
    }

    #endregion

    #region GetPostHistoryAsync Tests

    [Fact]
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
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
    }

    #endregion

    #region Edge Cases

    [Fact]
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
        discussion.LastActivityAt.Should().BeAfter(originalActivity ?? DateTime.MinValue);
    }

    [Fact]
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
        result.IsSuccess.Should().BeTrue();
        _mockPostRepository.Verify(r => r.UpdateAsync(post), Times.Once); // Soft delete
        _mockPostRepository.Verify(r => r.DeleteAsync(It.IsAny<Post>()), Times.Never);
    }

    #endregion
}
