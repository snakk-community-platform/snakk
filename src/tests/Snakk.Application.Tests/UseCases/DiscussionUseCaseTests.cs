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

public class DiscussionUseCaseTests
{
    private readonly Mock<IDiscussionRepository> _mockDiscussionRepository = new();
    private readonly Mock<ISpaceRepository> _mockSpaceRepository = new();
    private readonly Mock<IUserRepository> _mockUserRepository = new();
    private readonly Mock<IPostRepository> _mockPostRepository = new();
    private readonly Mock<IDomainEventDispatcher> _mockEventDispatcher = new();
    private readonly Mock<ICounterService> _mockCounterService = new();
    private readonly Mock<IMarkupParser> _mockMarkupParser = new();
    private readonly Mock<IRealtimeNotifier> _mockRealtimeNotifier = new();
    private readonly Mock<IMediaService> _mockMediaService = new();
    private DiscussionUseCase _useCase = null!;

    [Before(Test)]
    public void Setup()
    {
        _mockMarkupParser.Setup(m => m.ToHtml(It.IsAny<string>()))
            .Returns((string s) => $"<p>{s}</p>");

        _mockRealtimeNotifier
            .Setup(n => n.NotifyDiscussionCreatedAsync(It.IsAny<DiscussionId>(), It.IsAny<SpaceId>(), It.IsAny<User>()))
            .Returns(Task.CompletedTask);

        _mockRealtimeNotifier
            .Setup(n => n.NotifyDiscussionLockedAsync(It.IsAny<DiscussionId>()))
            .Returns(Task.CompletedTask);

        _mockRealtimeNotifier
            .Setup(n => n.NotifyDiscussionUnlockedAsync(It.IsAny<DiscussionId>()))
            .Returns(Task.CompletedTask);

        _mockRealtimeNotifier
            .Setup(n => n.NotifyDiscussionPinnedAsync(It.IsAny<DiscussionId>(), It.IsAny<SpaceId>(), It.IsAny<bool>()))
            .Returns(Task.CompletedTask);

        _mockRealtimeNotifier
            .Setup(n => n.NotifyDiscussionTitleUpdatedAsync(It.IsAny<DiscussionId>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _useCase = new DiscussionUseCase(
            _mockDiscussionRepository.Object,
            _mockSpaceRepository.Object,
            _mockUserRepository.Object,
            _mockPostRepository.Object,
            _mockEventDispatcher.Object,
            _mockCounterService.Object,
            _mockMarkupParser.Object,
            _mockRealtimeNotifier.Object,
            _mockMediaService.Object);
    }

    #region CreateDiscussionAsync Tests

    [Test]
    public async Task CreateDiscussionAsync_WithValidParameters_CreatesDiscussion()
    {
        // Arrange
        var spaceId = SpaceId.New();
        var userId = UserId.New();
        const string title = "Test Discussion";
        const string slug = "test-discussion";
        const string firstPostContent = "This is the first post content.";

        var space = Space.Create(HubId.New(), "Test Space", "test-space");
        var user = User.CreateWithEmail("TestUser", "test@example.com", "hash", "token");

        _mockSpaceRepository.Setup(r => r.GetByPublicIdAsync(spaceId))
            .ReturnsAsync(space);
        _mockUserRepository.Setup(r => r.GetByPublicIdAsync(userId))
            .ReturnsAsync(user);

        // Act
        var result = await _useCase.CreateDiscussionAsync(spaceId, userId, title, slug, firstPostContent);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsNotNull();
        await Assert.That(result.Value!.Title).IsEqualTo(title);
        await Assert.That(result.Value.Slug).IsEqualTo(slug);
        await Assert.That(result.Value.SpaceId).IsEqualTo(spaceId);
        await Assert.That(result.Value.CreatedByUserId).IsEqualTo(userId);

        _mockDiscussionRepository.Verify(r => r.AddAsync(It.IsAny<Discussion>()), Times.Once);
        _mockPostRepository.Verify(r => r.AddAsync(It.IsAny<Post>()), Times.Once);
        _mockCounterService.Verify(c => c.IncrementDiscussionCountAsync(spaceId), Times.Once);
        _mockCounterService.Verify(c => c.IncrementPostCountAsync(It.IsAny<DiscussionId>()), Times.Once);
        _mockEventDispatcher.Verify(d => d.DispatchAsync(It.IsAny<IEnumerable<IDomainEvent>>()), Times.Exactly(2));
    }

    [Test]
    public async Task CreateDiscussionAsync_WithNonExistentSpace_ReturnsFailure()
    {
        // Arrange
        var spaceId = SpaceId.New();
        var userId = UserId.New();

        _mockSpaceRepository.Setup(r => r.GetByPublicIdAsync(spaceId))
            .ReturnsAsync((Space?)null);

        // Act
        var result = await _useCase.CreateDiscussionAsync(spaceId, userId, "Title", "slug", "content");

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Space");
        await Assert.That(result.Error).Contains("not found");

        _mockDiscussionRepository.Verify(r => r.AddAsync(It.IsAny<Discussion>()), Times.Never);
    }

    [Test]
    public async Task CreateDiscussionAsync_WithNonExistentUser_ReturnsFailure()
    {
        // Arrange
        var spaceId = SpaceId.New();
        var userId = UserId.New();
        var space = Space.Create(HubId.New(), "Test Space", "test-space");

        _mockSpaceRepository.Setup(r => r.GetByPublicIdAsync(spaceId))
            .ReturnsAsync(space);
        _mockUserRepository.Setup(r => r.GetByPublicIdAsync(userId))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _useCase.CreateDiscussionAsync(spaceId, userId, "Title", "slug", "content");

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("User");
        await Assert.That(result.Error).Contains("not found");

        _mockDiscussionRepository.Verify(r => r.AddAsync(It.IsAny<Discussion>()), Times.Never);
    }

    [Test]
    public async Task CreateDiscussionAsync_ClearsDomainEventsAfterDispatching()
    {
        // Arrange
        var spaceId = SpaceId.New();
        var userId = UserId.New();
        var space = Space.Create(HubId.New(), "Test Space", "test-space");
        var user = User.CreateWithEmail("TestUser", "test@example.com", "hash", "token");

        _mockSpaceRepository.Setup(r => r.GetByPublicIdAsync(spaceId))
            .ReturnsAsync(space);
        _mockUserRepository.Setup(r => r.GetByPublicIdAsync(userId))
            .ReturnsAsync(user);

        // Act
        var result = await _useCase.CreateDiscussionAsync(spaceId, userId, "Title", "slug", "content");

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.DomainEvents).Count().IsEqualTo(0);
    }

    #endregion

    #region GetDiscussionAsync Tests

    [Test]
    public async Task GetDiscussionAsync_WithExistingDiscussion_ReturnsDiscussion()
    {
        // Arrange
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Test Discussion", "test-discussion");
        var discussionId = discussion.PublicId;

        _mockDiscussionRepository.Setup(r => r.GetByPublicIdAsync(discussionId))
            .ReturnsAsync(discussion);

        // Act
        var result = await _useCase.GetDiscussionAsync(discussionId);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsEqualTo(discussion);
    }

    [Test]
    public async Task GetDiscussionAsync_WithNonExistentDiscussion_ReturnsFailure()
    {
        // Arrange
        var discussionId = DiscussionId.New();

        _mockDiscussionRepository.Setup(r => r.GetByPublicIdAsync(discussionId))
            .ReturnsAsync((Discussion?)null);

        // Act
        var result = await _useCase.GetDiscussionAsync(discussionId);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("not found");
    }

    #endregion

    #region GetDiscussionsBySpaceAsync Tests

    [Test]
    public async Task GetDiscussionsBySpaceAsync_ReturnsPagedResults()
    {
        // Arrange
        var spaceId = SpaceId.New();
        var discussions = new List<Discussion>
        {
            Discussion.Create(spaceId, UserId.New(), "Discussion 1", "discussion-1"),
            Discussion.Create(spaceId, UserId.New(), "Discussion 2", "discussion-2")
        };

        var pagedResult = new PagedResult<Discussion>
        {
            Items = discussions,
            Offset = 0,
            PageSize = 20,
            HasMoreItems = false
        };

        _mockDiscussionRepository.Setup(r => r.GetBySpaceIdAsync(spaceId, 0, 20))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _useCase.GetDiscussionsBySpaceAsync(spaceId, 0, 20);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Items).Count().IsEqualTo(2);
    }

    #endregion

    #region UpdateDiscussionTitleAsync Tests

    [Test]
    public async Task UpdateDiscussionTitleAsync_WithValidParameters_UpdatesTitle()
    {
        // Arrange
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Old Title", "old-title");
        var discussionId = discussion.PublicId;
        const string newTitle = "New Title";

        _mockDiscussionRepository.Setup(r => r.GetByPublicIdAsync(discussionId))
            .ReturnsAsync(discussion);

        // Act
        var result = await _useCase.UpdateDiscussionTitleAsync(discussionId, newTitle);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Title).IsEqualTo(newTitle);

        _mockDiscussionRepository.Verify(r => r.UpdateAsync(discussion), Times.Once);
    }

    [Test]
    public async Task UpdateDiscussionTitleAsync_WithNonExistentDiscussion_ReturnsFailure()
    {
        // Arrange
        var discussionId = DiscussionId.New();

        _mockDiscussionRepository.Setup(r => r.GetByPublicIdAsync(discussionId))
            .ReturnsAsync((Discussion?)null);

        // Act
        var result = await _useCase.UpdateDiscussionTitleAsync(discussionId, "New Title");

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("not found");
    }

    [Test]
    public async Task UpdateDiscussionTitleAsync_WithLockedDiscussion_ReturnsFailure()
    {
        // Arrange
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Old Title", "old-title");
        discussion.Lock();
        var discussionId = discussion.PublicId;

        _mockDiscussionRepository.Setup(r => r.GetByPublicIdAsync(discussionId))
            .ReturnsAsync(discussion);

        // Act
        var result = await _useCase.UpdateDiscussionTitleAsync(discussionId, "New Title");

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();

        _mockDiscussionRepository.Verify(r => r.UpdateAsync(It.IsAny<Discussion>()), Times.Never);
    }

    #endregion

    #region PinDiscussionAsync Tests

    [Test]
    public async Task PinDiscussionAsync_WithExistingDiscussion_PinsDiscussion()
    {
        // Arrange
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Test Discussion", "test-discussion");
        var discussionId = discussion.PublicId;

        _mockDiscussionRepository.Setup(r => r.GetByPublicIdAsync(discussionId))
            .ReturnsAsync(discussion);

        // Act
        var result = await _useCase.PinDiscussionAsync(discussionId);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(discussion.IsPinned).IsTrue();

        _mockDiscussionRepository.Verify(r => r.UpdateAsync(discussion), Times.Once);
    }

    [Test]
    public async Task PinDiscussionAsync_WithNonExistentDiscussion_ReturnsFailure()
    {
        // Arrange
        var discussionId = DiscussionId.New();

        _mockDiscussionRepository.Setup(r => r.GetByPublicIdAsync(discussionId))
            .ReturnsAsync((Discussion?)null);

        // Act
        var result = await _useCase.PinDiscussionAsync(discussionId);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("not found");
    }

    #endregion

    #region UnpinDiscussionAsync Tests

    [Test]
    public async Task UnpinDiscussionAsync_WithPinnedDiscussion_UnpinsDiscussion()
    {
        // Arrange
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Test Discussion", "test-discussion");
        discussion.Pin();
        var discussionId = discussion.PublicId;

        _mockDiscussionRepository.Setup(r => r.GetByPublicIdAsync(discussionId))
            .ReturnsAsync(discussion);

        // Act
        var result = await _useCase.UnpinDiscussionAsync(discussionId);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(discussion.IsPinned).IsFalse();

        _mockDiscussionRepository.Verify(r => r.UpdateAsync(discussion), Times.Once);
    }

    [Test]
    public async Task UnpinDiscussionAsync_WithNonExistentDiscussion_ReturnsFailure()
    {
        // Arrange
        var discussionId = DiscussionId.New();

        _mockDiscussionRepository.Setup(r => r.GetByPublicIdAsync(discussionId))
            .ReturnsAsync((Discussion?)null);

        // Act
        var result = await _useCase.UnpinDiscussionAsync(discussionId);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("not found");
    }

    #endregion

    #region LockDiscussionAsync Tests

    [Test]
    public async Task LockDiscussionAsync_WithExistingDiscussion_LocksDiscussion()
    {
        // Arrange
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Test Discussion", "test-discussion");
        var discussionId = discussion.PublicId;

        _mockDiscussionRepository.Setup(r => r.GetByPublicIdAsync(discussionId))
            .ReturnsAsync(discussion);

        // Act
        var result = await _useCase.LockDiscussionAsync(discussionId);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(discussion.IsLocked).IsTrue();

        _mockDiscussionRepository.Verify(r => r.UpdateAsync(discussion), Times.Once);
    }

    [Test]
    public async Task LockDiscussionAsync_WithNonExistentDiscussion_ReturnsFailure()
    {
        // Arrange
        var discussionId = DiscussionId.New();

        _mockDiscussionRepository.Setup(r => r.GetByPublicIdAsync(discussionId))
            .ReturnsAsync((Discussion?)null);

        // Act
        var result = await _useCase.LockDiscussionAsync(discussionId);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("not found");
    }

    #endregion

    #region UnlockDiscussionAsync Tests

    [Test]
    public async Task UnlockDiscussionAsync_WithLockedDiscussion_UnlocksDiscussion()
    {
        // Arrange
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Test Discussion", "test-discussion");
        discussion.Lock();
        var discussionId = discussion.PublicId;

        _mockDiscussionRepository.Setup(r => r.GetByPublicIdAsync(discussionId))
            .ReturnsAsync(discussion);

        // Act
        var result = await _useCase.UnlockDiscussionAsync(discussionId);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(discussion.IsLocked).IsFalse();

        _mockDiscussionRepository.Verify(r => r.UpdateAsync(discussion), Times.Once);
    }

    [Test]
    public async Task UnlockDiscussionAsync_WithNonExistentDiscussion_ReturnsFailure()
    {
        // Arrange
        var discussionId = DiscussionId.New();

        _mockDiscussionRepository.Setup(r => r.GetByPublicIdAsync(discussionId))
            .ReturnsAsync((Discussion?)null);

        // Act
        var result = await _useCase.UnlockDiscussionAsync(discussionId);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("not found");
    }

    #endregion

    #region GetPostNumberAsync Tests

    [Test]
    public async Task GetPostNumberAsync_WithValidParameters_ReturnsPostNumber()
    {
        // Arrange
        var discussionId = DiscussionId.New();
        var postId = PostId.New();
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Test Discussion", "test-discussion");
        var post = Post.Create(discussion.PublicId, UserId.New(), "Post content", "<p>Post content</p>");

        _mockDiscussionRepository.Setup(r => r.GetByPublicIdAsync(discussionId))
            .ReturnsAsync(discussion);
        _mockPostRepository.Setup(r => r.GetByPublicIdAsync(postId))
            .ReturnsAsync(post);
        _mockPostRepository.Setup(r => r.GetPostNumberInDiscussionAsync(discussionId, post.CreatedAt))
            .ReturnsAsync(3);

        // We need the post's DiscussionId to match. Let's use the discussion's PublicId.
        var matchingPost = Post.Create(discussionId, UserId.New(), "Post content", "<p>Post content</p>");
        // But the post returned from repo must have DiscussionId == discussion.PublicId
        // Since we're using Moq, we need proper setup:
        var realDiscussion = Discussion.Create(SpaceId.New(), UserId.New(), "Test", "test");
        var realPost = Post.Create(realDiscussion.PublicId, UserId.New(), "content", "<p>content</p>");

        _mockDiscussionRepository.Setup(r => r.GetByPublicIdAsync(realDiscussion.PublicId))
            .ReturnsAsync(realDiscussion);
        _mockPostRepository.Setup(r => r.GetByPublicIdAsync(realPost.PublicId))
            .ReturnsAsync(realPost);
        _mockPostRepository.Setup(r => r.GetPostNumberInDiscussionAsync(realDiscussion.PublicId, realPost.CreatedAt))
            .ReturnsAsync(5);

        // Act
        var result = await _useCase.GetPostNumberAsync(realDiscussion.PublicId, realPost.PublicId);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsEqualTo(5);
    }

    [Test]
    public async Task GetPostNumberAsync_WithNonExistentDiscussion_ReturnsFailure()
    {
        // Arrange
        var discussionId = DiscussionId.New();
        var postId = PostId.New();

        _mockDiscussionRepository.Setup(r => r.GetByPublicIdAsync(discussionId))
            .ReturnsAsync((Discussion?)null);

        // Act
        var result = await _useCase.GetPostNumberAsync(discussionId, postId);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Discussion not found");
    }

    [Test]
    public async Task GetPostNumberAsync_WithNonExistentPost_ReturnsFailure()
    {
        // Arrange
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Test", "test");
        var postId = PostId.New();

        _mockDiscussionRepository.Setup(r => r.GetByPublicIdAsync(discussion.PublicId))
            .ReturnsAsync(discussion);
        _mockPostRepository.Setup(r => r.GetByPublicIdAsync(postId))
            .ReturnsAsync((Post?)null);

        // Act
        var result = await _useCase.GetPostNumberAsync(discussion.PublicId, postId);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Post not found");
    }

    [Test]
    public async Task GetPostNumberAsync_WithPostInDifferentDiscussion_ReturnsFailure()
    {
        // Arrange
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Discussion 1", "discussion-1");
        var otherDiscussion = Discussion.Create(SpaceId.New(), UserId.New(), "Discussion 2", "discussion-2");
        var post = Post.Create(otherDiscussion.PublicId, UserId.New(), "Post in other discussion", "<p>Post in other discussion</p>");

        _mockDiscussionRepository.Setup(r => r.GetByPublicIdAsync(discussion.PublicId))
            .ReturnsAsync(discussion);
        _mockPostRepository.Setup(r => r.GetByPublicIdAsync(post.PublicId))
            .ReturnsAsync(post);

        // Act
        var result = await _useCase.GetPostNumberAsync(discussion.PublicId, post.PublicId);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Post does not belong to this discussion");
    }

    #endregion

    #region GetFirstPostPreviewAsync Tests

    [Test]
    public async Task GetFirstPostPreviewAsync_WithExistingFirstPost_ReturnsContent()
    {
        // Arrange
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Test", "test");
        var firstPost = Post.Create(discussion.PublicId, UserId.New(), "This is the first post content", "<p>This is the first post content</p>", isFirstPost: true);

        _mockDiscussionRepository.Setup(r => r.GetByPublicIdAsync(discussion.PublicId))
            .ReturnsAsync(discussion);
        _mockPostRepository.Setup(r => r.GetFirstPostByDiscussionIdAsync(discussion.PublicId))
            .ReturnsAsync(firstPost);

        // Act
        var result = await _useCase.GetFirstPostPreviewAsync(discussion.PublicId);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsEqualTo("This is the first post content");
    }

    [Test]
    public async Task GetFirstPostPreviewAsync_WithNonExistentDiscussion_ReturnsFailure()
    {
        // Arrange
        var discussionId = DiscussionId.New();

        _mockDiscussionRepository.Setup(r => r.GetByPublicIdAsync(discussionId))
            .ReturnsAsync((Discussion?)null);

        // Act
        var result = await _useCase.GetFirstPostPreviewAsync(discussionId);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Discussion not found");
    }

    [Test]
    public async Task GetFirstPostPreviewAsync_WithNoFirstPost_ReturnsFailure()
    {
        // Arrange
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Test", "test");

        _mockDiscussionRepository.Setup(r => r.GetByPublicIdAsync(discussion.PublicId))
            .ReturnsAsync(discussion);
        _mockPostRepository.Setup(r => r.GetFirstPostByDiscussionIdAsync(discussion.PublicId))
            .ReturnsAsync((Post?)null);

        // Act
        var result = await _useCase.GetFirstPostPreviewAsync(discussion.PublicId);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("First post not found");
    }

    #endregion
}
