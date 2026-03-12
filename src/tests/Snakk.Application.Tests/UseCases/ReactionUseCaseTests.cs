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
        var type = ReactionType.Love;

        var post = Post.Create(discussionId, UserId.New(), "Test content", "<p>Test content</p>");
        var reactionCounts = new Dictionary<ReactionType, int> { { ReactionType.Love, 1 } };

        _mockPostRepository.Setup(r => r.GetByPublicIdAsync(postId))
            .ReturnsAsync(post);
        _mockReactionRepository.Setup(r => r.GetByUserAndPostAsync(userId, postId))
            .ReturnsAsync((Reaction?)null);
        _mockReactionRepository.Setup(r => r.GetCountsByPostIdAsync(postId))
            .ReturnsAsync(reactionCounts);

        // Act
        var result = await _useCase.ToggleReactionAsync(postId, userId, type);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsTrue();

        _mockReactionRepository.Verify(r => r.AddAsync(It.IsAny<Reaction>()), Times.Once);
        _mockReactionRepository.Verify(r => r.DeleteAsync(It.IsAny<Reaction>()), Times.Never);
        _mockCounterService.Verify(c => c.IncrementReactionCountAsync(postId, discussionId), Times.Once);
        _mockRealtimeNotifier.Verify(n => n.NotifyReactionUpdatedAsync(postId, discussionId, reactionCounts), Times.Once);
    }

    [Test]
    public async Task ToggleReactionAsync_WithExistingReaction_RemovesReaction()
    {
        // Arrange
        var postId = PostId.New();
        var userId = UserId.New();
        var discussionId = DiscussionId.New();
        var type = ReactionType.Agree;

        var post = Post.Create(discussionId, UserId.New(), "Test content", "<p>Test content</p>");
        var existingReaction = Reaction.Create(postId, userId, type);
        var reactionCounts = new Dictionary<ReactionType, int>();

        _mockPostRepository.Setup(r => r.GetByPublicIdAsync(postId))
            .ReturnsAsync(post);
        _mockReactionRepository.Setup(r => r.GetByUserAndPostAsync(userId, postId))
            .ReturnsAsync(existingReaction);
        _mockReactionRepository.Setup(r => r.GetCountsByPostIdAsync(postId))
            .ReturnsAsync(reactionCounts);

        // Act
        var result = await _useCase.ToggleReactionAsync(postId, userId, type);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsFalse();

        _mockReactionRepository.Verify(r => r.DeleteAsync(existingReaction), Times.Once);
        _mockReactionRepository.Verify(r => r.AddAsync(It.IsAny<Reaction>()), Times.Never);
        _mockCounterService.Verify(c => c.DecrementReactionCountAsync(postId, discussionId), Times.Once);
        _mockRealtimeNotifier.Verify(n => n.NotifyReactionUpdatedAsync(postId, discussionId, reactionCounts), Times.Once);
    }

    [Test]
    public async Task ToggleReactionAsync_AddingDifferentType_ReplacesExisting()
    {
        // Arrange - user already has Agree, now adding Love → Love replaces Agree, counter unchanged
        var postId = PostId.New();
        var userId = UserId.New();
        var discussionId = DiscussionId.New();
        var existingType = ReactionType.Agree;
        var newType = ReactionType.Love;

        var post = Post.Create(discussionId, UserId.New(), "Test content", "<p>Test content</p>");
        var existingReaction = Reaction.Create(postId, userId, existingType);
        var reactionCounts = new Dictionary<ReactionType, int> { { ReactionType.Love, 1 } };

        _mockPostRepository.Setup(r => r.GetByPublicIdAsync(postId))
            .ReturnsAsync(post);
        _mockReactionRepository.Setup(r => r.GetByUserAndPostAsync(userId, postId))
            .ReturnsAsync(existingReaction);
        _mockReactionRepository.Setup(r => r.GetCountsByPostIdAsync(postId))
            .ReturnsAsync(reactionCounts);

        // Act
        var result = await _useCase.ToggleReactionAsync(postId, userId, newType);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsTrue();

        _mockReactionRepository.Verify(r => r.DeleteAsync(existingReaction), Times.Once);
        _mockReactionRepository.Verify(r => r.AddAsync(It.IsAny<Reaction>()), Times.Once);
        // Counter does not change on replace — no increment or decrement
        _mockCounterService.Verify(c => c.IncrementReactionCountAsync(postId, discussionId), Times.Never);
        _mockCounterService.Verify(c => c.DecrementReactionCountAsync(postId, discussionId), Times.Never);
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
        var result = await _useCase.ToggleReactionAsync(postId, userId, ReactionType.Love);

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

        // Act & Assert - All 9 reaction types should work
        foreach (var type in Enum.GetValues<ReactionType>())
        {
            var result = await _useCase.ToggleReactionAsync(postId, userId, type);
            await Assert.That(result.IsSuccess).IsTrue();
        }

        _mockReactionRepository.Verify(r => r.AddAsync(It.IsAny<Reaction>()), Times.Exactly(9));
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
            { ReactionType.Agree, 5 },
            { ReactionType.Love, 3 },
            { ReactionType.Watching, 1 }
        };

        _mockReactionRepository.Setup(r => r.GetCountsByPostIdAsync(postId))
            .ReturnsAsync(counts);

        // Act
        var result = await _useCase.GetReactionCountsAsync(postId);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result).Count().IsEqualTo(3);
        await Assert.That(result[ReactionType.Agree]).IsEqualTo(5);
        await Assert.That(result[ReactionType.Love]).IsEqualTo(3);
        await Assert.That(result[ReactionType.Watching]).IsEqualTo(1);
    }

    #endregion

    #region GetUserReactionsAsync Tests

    [Test]
    public async Task GetUserReactionsAsync_WithExistingReactions_ReturnsReactionTypes()
    {
        // Arrange
        var postId = PostId.New();
        var userId = UserId.New();
        var types = new List<ReactionType> { ReactionType.Love, ReactionType.Fire };

        _mockReactionRepository.Setup(r => r.GetUserReactionsForPostAsync(userId, postId))
            .ReturnsAsync(types);

        // Act
        var result = await _useCase.GetUserReactionsAsync(postId, userId);

        // Assert
        await Assert.That(result).Count().IsEqualTo(2);
        await Assert.That(result).Contains(ReactionType.Love);
        await Assert.That(result).Contains(ReactionType.Fire);
    }

    [Test]
    public async Task GetUserReactionsAsync_WithNoReactions_ReturnsEmptyList()
    {
        // Arrange
        var postId = PostId.New();
        var userId = UserId.New();

        _mockReactionRepository.Setup(r => r.GetUserReactionsForPostAsync(userId, postId))
            .ReturnsAsync(new List<ReactionType>());

        // Act
        var result = await _useCase.GetUserReactionsAsync(postId, userId);

        // Assert
        await Assert.That(result).IsEmpty();
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
            { postIds[0].Value, new Dictionary<ReactionType, int> { { ReactionType.Agree, 3 } } },
            { postIds[1].Value, new Dictionary<ReactionType, int> { { ReactionType.Love, 5 } } },
            { postIds[2].Value, new Dictionary<ReactionType, int>() }
        };

        _mockReactionRepository.Setup(r => r.GetCountsByPostIdsAsync(postIds))
            .ReturnsAsync(batchCounts);

        // Act
        var result = await _useCase.GetReactionCountsBatchAsync(postIds);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result).Count().IsEqualTo(3);
        await Assert.That(result[postIds[0].Value][ReactionType.Agree]).IsEqualTo(3);
        await Assert.That(result[postIds[1].Value][ReactionType.Love]).IsEqualTo(5);
        await Assert.That(result[postIds[2].Value]).IsEmpty();
    }

    [Test]
    public async Task GetUserReactionsBatchAsync_ReturnsUserReactionsForMultiplePosts()
    {
        // Arrange
        var userId = UserId.New();
        var postIds = new List<PostId> { PostId.New(), PostId.New(), PostId.New() };
        var userReactions = new Dictionary<string, List<ReactionType>>
        {
            { postIds[0].Value, new List<ReactionType> { ReactionType.Agree, ReactionType.Fire } },
            { postIds[2].Value, new List<ReactionType> { ReactionType.Watching } }
            // postIds[1] has no user reactions
        };

        _mockReactionRepository.Setup(r => r.GetUserReactionsForPostsAsync(userId, postIds))
            .ReturnsAsync(userReactions);

        // Act
        var result = await _useCase.GetUserReactionsBatchAsync(userId, postIds);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result).Count().IsEqualTo(2);
        await Assert.That(result[postIds[0].Value]).Count().IsEqualTo(2);
        await Assert.That(result[postIds[2].Value]).Contains(ReactionType.Watching);
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
        var type = ReactionType.Love;
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
    public async Task ToggleReactionAsync_DifferentType_ReplacesExistingReaction()
    {
        // Arrange - first add Agree (no existing), then add Love (Agree exists → Love replaces Agree)
        var postId = PostId.New();
        var userId = UserId.New();
        var discussionId = DiscussionId.New();
        var post = Post.Create(discussionId, UserId.New(), "Test content", "<p>Test content</p>");

        _mockPostRepository.Setup(r => r.GetByPublicIdAsync(postId))
            .ReturnsAsync(post);
        _mockReactionRepository.Setup(r => r.GetCountsByPostIdAsync(postId))
            .ReturnsAsync(new Dictionary<ReactionType, int>());

        // First call - no existing reaction
        _mockReactionRepository.Setup(r => r.GetByUserAndPostAsync(userId, postId))
            .ReturnsAsync((Reaction?)null);

        var agreeResult = await _useCase.ToggleReactionAsync(postId, userId, ReactionType.Agree);

        // Second call - Agree exists, Love should replace it
        var agreeReaction = Reaction.Create(postId, userId, ReactionType.Agree);
        _mockReactionRepository.Setup(r => r.GetByUserAndPostAsync(userId, postId))
            .ReturnsAsync(agreeReaction);

        var loveResult = await _useCase.ToggleReactionAsync(postId, userId, ReactionType.Love);

        // Assert
        await Assert.That(agreeResult.IsSuccess).IsTrue();
        await Assert.That(agreeResult.Value).IsTrue();
        await Assert.That(loveResult.IsSuccess).IsTrue();
        await Assert.That(loveResult.Value).IsTrue();

        // 2 adds (Agree + Love), 1 delete (Agree removed on replace)
        _mockReactionRepository.Verify(r => r.AddAsync(It.IsAny<Reaction>()), Times.Exactly(2));
        _mockReactionRepository.Verify(r => r.DeleteAsync(It.IsAny<Reaction>()), Times.Once);
        // Only the first add increments the counter; the replace keeps it unchanged
        _mockCounterService.Verify(c => c.IncrementReactionCountAsync(postId, discussionId), Times.Once);
        _mockCounterService.Verify(c => c.DecrementReactionCountAsync(postId, discussionId), Times.Never);
    }

    #endregion
}
