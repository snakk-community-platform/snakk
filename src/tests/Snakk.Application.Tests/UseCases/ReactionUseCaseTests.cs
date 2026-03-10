using Moq;
using Snakk.Application.Services;
using Snakk.Application.UseCases;
using Snakk.Domain.Entities;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;

namespace Snakk.Application.Tests.UseCases;

public class ReactionUseCaseTests
{
    private readonly Mock<IReactionRepository> _mockReactionRepository = new();
    private readonly Mock<IPostRepository> _mockPostRepository = new();
    private readonly Mock<IRealtimeNotifier> _mockRealtimeNotifier = new();
    private readonly Mock<ICounterService> _mockCounterService = new();
    private ReactionUseCase _useCase = null!;

    [Before(Test)]
    public void Setup()
    {
        _useCase = new ReactionUseCase(
            _mockReactionRepository.Object,
            _mockPostRepository.Object,
            _mockRealtimeNotifier.Object,
            _mockCounterService.Object);
    }

    #region ToggleReactionAsync Tests

    [Test]
    public async Task ToggleReactionAsync_WithNoExistingReaction_AddsReaction()
    {
        // Arrange
        var postId = PostId.New();
        var userId = UserId.New();
        var discussionId = DiscussionId.New();
        var type = ReactionType.Heart;

        var post = Post.Create(discussionId, UserId.New(), "Test content", "<p>Test content</p>");
        var reactionCounts = new Dictionary<ReactionType, int> { { ReactionType.Heart, 1 } };

        _mockPostRepository.Setup(r => r.GetByPublicIdAsync(postId))
            .ReturnsAsync(post);
        _mockReactionRepository.Setup(r => r.GetByUserAndPostAsync(userId, postId))
            .ReturnsAsync((Reaction?)null); // No existing reaction
        _mockReactionRepository.Setup(r => r.GetCountsByPostIdAsync(postId))
            .ReturnsAsync(reactionCounts);

        // Act
        var result = await _useCase.ToggleReactionAsync(postId, userId, type);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsTrue();

        _mockReactionRepository.Verify(r => r.AddAsync(It.IsAny<Reaction>()), Times.Once);
        _mockReactionRepository.Verify(r => r.DeleteAsync(It.IsAny<Reaction>()), Times.Never);
        _mockCounterService.Verify(c => c.IncrementUniqueReactorCountAsync(discussionId, userId), Times.Once);
        _mockRealtimeNotifier.Verify(n => n.NotifyReactionUpdatedAsync(postId, discussionId, reactionCounts), Times.Once);
    }

    [Test]
    public async Task ToggleReactionAsync_WithSameExistingReaction_RemovesReaction()
    {
        // Arrange
        var postId = PostId.New();
        var userId = UserId.New();
        var discussionId = DiscussionId.New();
        var type = ReactionType.ThumbsUp;

        var post = Post.Create(discussionId, UserId.New(), "Test content", "<p>Test content</p>");
        var existingReaction = Reaction.Create(postId, userId, type);
        var reactionCounts = new Dictionary<ReactionType, int>();

        _mockPostRepository.Setup(r => r.GetByPublicIdAsync(postId))
            .ReturnsAsync(post);
        _mockReactionRepository.Setup(r => r.GetByUserAndPostAsync(userId, postId))
            .ReturnsAsync(existingReaction); // Existing same reaction
        _mockReactionRepository.Setup(r => r.GetCountsByPostIdAsync(postId))
            .ReturnsAsync(reactionCounts);

        // Act
        var result = await _useCase.ToggleReactionAsync(postId, userId, type);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsFalse();

        _mockReactionRepository.Verify(r => r.DeleteAsync(existingReaction), Times.Once);
        _mockReactionRepository.Verify(r => r.AddAsync(It.IsAny<Reaction>()), Times.Never);
        _mockCounterService.Verify(c => c.DecrementUniqueReactorCountAsync(discussionId, userId), Times.Once);
        _mockRealtimeNotifier.Verify(n => n.NotifyReactionUpdatedAsync(postId, discussionId, reactionCounts), Times.Once);
    }

    [Test]
    public async Task ToggleReactionAsync_WithDifferentExistingReaction_ChangesReaction()
    {
        // Arrange
        var postId = PostId.New();
        var userId = UserId.New();
        var discussionId = DiscussionId.New();
        var oldType = ReactionType.ThumbsUp;
        var newType = ReactionType.Heart;

        var post = Post.Create(discussionId, UserId.New(), "Test content", "<p>Test content</p>");
        var existingReaction = Reaction.Create(postId, userId, oldType);
        var reactionCounts = new Dictionary<ReactionType, int> { { ReactionType.Heart, 1 } };

        _mockPostRepository.Setup(r => r.GetByPublicIdAsync(postId))
            .ReturnsAsync(post);
        _mockReactionRepository.Setup(r => r.GetByUserAndPostAsync(userId, postId))
            .ReturnsAsync(existingReaction); // Existing different reaction
        _mockReactionRepository.Setup(r => r.GetCountsByPostIdAsync(postId))
            .ReturnsAsync(reactionCounts);

        // Act
        var result = await _useCase.ToggleReactionAsync(postId, userId, newType);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsTrue();

        _mockReactionRepository.Verify(r => r.DeleteAsync(existingReaction), Times.Once); // Remove old
        _mockReactionRepository.Verify(r => r.AddAsync(It.IsAny<Reaction>()), Times.Once); // Add new
        _mockCounterService.Verify(c => c.IncrementUniqueReactorCountAsync(discussionId, userId), Times.Once);
        _mockRealtimeNotifier.Verify(n => n.NotifyReactionUpdatedAsync(postId, discussionId, reactionCounts), Times.Once);
    }

    [Test]
    public async Task ToggleReactionAsync_WithNonExistentPost_ReturnsFailure()
    {
        // Arrange
        var postId = PostId.New();
        var userId = UserId.New();

        _mockPostRepository.Setup(r => r.GetByPublicIdAsync(postId))
            .ReturnsAsync((Post?)null);

        // Act
        var result = await _useCase.ToggleReactionAsync(postId, userId, ReactionType.Heart);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Post not found");
        _mockReactionRepository.Verify(r => r.AddAsync(It.IsAny<Reaction>()), Times.Never);
        _mockReactionRepository.Verify(r => r.DeleteAsync(It.IsAny<Reaction>()), Times.Never);
    }

    [Test]
    public async Task ToggleReactionAsync_AllReactionTypes_AreSupported()
    {
        // Arrange
        var postId = PostId.New();
        var userId = UserId.New();
        var discussionId = DiscussionId.New();
        var post = Post.Create(discussionId, UserId.New(), "Test content", "<p>Test content</p>");

        _mockPostRepository.Setup(r => r.GetByPublicIdAsync(postId))
            .ReturnsAsync(post);
        _mockReactionRepository.Setup(r => r.GetByUserAndPostAsync(userId, postId))
            .ReturnsAsync((Reaction?)null);
        _mockReactionRepository.Setup(r => r.GetCountsByPostIdAsync(postId))
            .ReturnsAsync(new Dictionary<ReactionType, int>());

        // Act & Assert - All reaction types should work
        var thumbsUpResult = await _useCase.ToggleReactionAsync(postId, userId, ReactionType.ThumbsUp);
        await Assert.That(thumbsUpResult.IsSuccess).IsTrue();

        var heartResult = await _useCase.ToggleReactionAsync(postId, userId, ReactionType.Heart);
        await Assert.That(heartResult.IsSuccess).IsTrue();

        var eyesResult = await _useCase.ToggleReactionAsync(postId, userId, ReactionType.Eyes);
        await Assert.That(eyesResult.IsSuccess).IsTrue();

        _mockReactionRepository.Verify(r => r.AddAsync(It.IsAny<Reaction>()), Times.Exactly(3));
    }

    #endregion

    #region GetReactionCountsAsync Tests

    [Test]
    public async Task GetReactionCountsAsync_ReturnsCounts()
    {
        // Arrange
        var postId = PostId.New();
        var counts = new Dictionary<ReactionType, int>
        {
            { ReactionType.ThumbsUp, 5 },
            { ReactionType.Heart, 3 },
            { ReactionType.Eyes, 1 }
        };

        _mockReactionRepository.Setup(r => r.GetCountsByPostIdAsync(postId))
            .ReturnsAsync(counts);

        // Act
        var result = await _useCase.GetReactionCountsAsync(postId);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result).Count().IsEqualTo(3);
        await Assert.That(result[ReactionType.ThumbsUp]).IsEqualTo(5);
        await Assert.That(result[ReactionType.Heart]).IsEqualTo(3);
        await Assert.That(result[ReactionType.Eyes]).IsEqualTo(1);
    }

    #endregion

    #region GetUserReactionAsync Tests

    [Test]
    public async Task GetUserReactionAsync_WithExistingReaction_ReturnsReactionType()
    {
        // Arrange
        var postId = PostId.New();
        var userId = UserId.New();
        var type = ReactionType.Heart;

        _mockReactionRepository.Setup(r => r.GetUserReactionForPostAsync(userId, postId))
            .ReturnsAsync(type);

        // Act
        var result = await _useCase.GetUserReactionAsync(postId, userId);

        // Assert
        await Assert.That(result).IsEqualTo(type);
    }

    [Test]
    public async Task GetUserReactionAsync_WithNoReaction_ReturnsNull()
    {
        // Arrange
        var postId = PostId.New();
        var userId = UserId.New();

        _mockReactionRepository.Setup(r => r.GetUserReactionForPostAsync(userId, postId))
            .ReturnsAsync((ReactionType?)null);

        // Act
        var result = await _useCase.GetUserReactionAsync(postId, userId);

        // Assert
        await Assert.That(result).IsNull();
    }

    #endregion

    #region Batch Methods Tests

    [Test]
    public async Task GetReactionCountsBatchAsync_ReturnsCountsForMultiplePosts()
    {
        // Arrange
        var postIds = new List<PostId> { PostId.New(), PostId.New(), PostId.New() };
        var batchCounts = new Dictionary<string, Dictionary<ReactionType, int>>
        {
            { postIds[0].Value, new Dictionary<ReactionType, int> { { ReactionType.ThumbsUp, 3 } } },
            { postIds[1].Value, new Dictionary<ReactionType, int> { { ReactionType.Heart, 5 } } },
            { postIds[2].Value, new Dictionary<ReactionType, int>() }
        };

        _mockReactionRepository.Setup(r => r.GetCountsByPostIdsAsync(postIds))
            .ReturnsAsync(batchCounts);

        // Act
        var result = await _useCase.GetReactionCountsBatchAsync(postIds);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result).Count().IsEqualTo(3);
        await Assert.That(result[postIds[0].Value][ReactionType.ThumbsUp]).IsEqualTo(3);
        await Assert.That(result[postIds[1].Value][ReactionType.Heart]).IsEqualTo(5);
        await Assert.That(result[postIds[2].Value]).IsEmpty();
    }

    [Test]
    public async Task GetUserReactionsBatchAsync_ReturnsUserReactionsForMultiplePosts()
    {
        // Arrange
        var userId = UserId.New();
        var postIds = new List<PostId> { PostId.New(), PostId.New(), PostId.New() };
        var userReactions = new Dictionary<string, ReactionType>
        {
            { postIds[0].Value, ReactionType.ThumbsUp },
            { postIds[2].Value, ReactionType.Eyes }
            // postIds[1] has no user reaction
        };

        _mockReactionRepository.Setup(r => r.GetUserReactionsForPostsAsync(userId, postIds))
            .ReturnsAsync(userReactions);

        // Act
        var result = await _useCase.GetUserReactionsBatchAsync(userId, postIds);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result).Count().IsEqualTo(2);
        await Assert.That(result[postIds[0].Value]).IsEqualTo(ReactionType.ThumbsUp);
        await Assert.That(result[postIds[2].Value]).IsEqualTo(ReactionType.Eyes);
        await Assert.That(result.ContainsKey(postIds[1].Value)).IsFalse();
    }

    #endregion

    #region Edge Cases

    [Test]
    public async Task ToggleReactionAsync_ToggleTwice_AddsAndRemoves()
    {
        // Arrange
        var postId = PostId.New();
        var userId = UserId.New();
        var discussionId = DiscussionId.New();
        var type = ReactionType.Heart;
        var post = Post.Create(discussionId, UserId.New(), "Test content", "<p>Test content</p>");

        _mockPostRepository.Setup(r => r.GetByPublicIdAsync(postId))
            .ReturnsAsync(post);
        _mockReactionRepository.Setup(r => r.GetCountsByPostIdAsync(postId))
            .ReturnsAsync(new Dictionary<ReactionType, int>());

        // First call - no existing reaction
        _mockReactionRepository.Setup(r => r.GetByUserAndPostAsync(userId, postId))
            .ReturnsAsync((Reaction?)null);

        // Act - First toggle (add)
        var firstResult = await _useCase.ToggleReactionAsync(postId, userId, type);

        // Assert first toggle
        await Assert.That(firstResult.IsSuccess).IsTrue();
        await Assert.That(firstResult.Value).IsTrue();

        // Arrange - Second call - now there's an existing reaction
        var addedReaction = Reaction.Create(postId, userId, type);
        _mockReactionRepository.Setup(r => r.GetByUserAndPostAsync(userId, postId))
            .ReturnsAsync(addedReaction);

        // Act - Second toggle (remove)
        var secondResult = await _useCase.ToggleReactionAsync(postId, userId, type);

        // Assert second toggle
        await Assert.That(secondResult.IsSuccess).IsTrue();
        await Assert.That(secondResult.Value).IsFalse();

        _mockReactionRepository.Verify(r => r.AddAsync(It.IsAny<Reaction>()), Times.Once);
        _mockReactionRepository.Verify(r => r.DeleteAsync(It.IsAny<Reaction>()), Times.Once);
    }

    [Test]
    public async Task ToggleReactionAsync_ChangingReactionType_DeletesOldAndAddsNew()
    {
        // Arrange
        var postId = PostId.New();
        var userId = UserId.New();
        var discussionId = DiscussionId.New();
        var post = Post.Create(discussionId, UserId.New(), "Test content", "<p>Test content</p>");

        var thumbsUpReaction = Reaction.Create(postId, userId, ReactionType.ThumbsUp);

        _mockPostRepository.Setup(r => r.GetByPublicIdAsync(postId))
            .ReturnsAsync(post);
        _mockReactionRepository.Setup(r => r.GetByUserAndPostAsync(userId, postId))
            .ReturnsAsync(thumbsUpReaction);
        _mockReactionRepository.Setup(r => r.GetCountsByPostIdAsync(postId))
            .ReturnsAsync(new Dictionary<ReactionType, int>());

        // Act - Change to Heart
        var result = await _useCase.ToggleReactionAsync(postId, userId, ReactionType.Heart);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsTrue();

        _mockReactionRepository.Verify(r => r.DeleteAsync(thumbsUpReaction), Times.Once);
        _mockReactionRepository.Verify(r => r.AddAsync(It.Is<Reaction>(r => r.Type == ReactionType.Heart)), Times.Once);
    }

    #endregion
}
