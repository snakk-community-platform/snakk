using FluentAssertions;
using Moq;
using Snakk.Application.Services;
using Snakk.Application.UseCases;
using Snakk.Domain.Entities;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;

namespace Snakk.Application.Tests.UseCases;

public class ReactionUseCaseTests
{
    private readonly Mock<IReactionRepository> _mockReactionRepository;
    private readonly Mock<IPostRepository> _mockPostRepository;
    private readonly Mock<IRealtimeNotifier> _mockRealtimeNotifier;
    private readonly Mock<ICounterService> _mockCounterService;
    private readonly ReactionUseCase _useCase;

    public ReactionUseCaseTests()
    {
        _mockReactionRepository = new Mock<IReactionRepository>();
        _mockPostRepository = new Mock<IPostRepository>();
        _mockRealtimeNotifier = new Mock<IRealtimeNotifier>();
        _mockCounterService = new Mock<ICounterService>();

        _useCase = new ReactionUseCase(
            _mockReactionRepository.Object,
            _mockPostRepository.Object,
            _mockRealtimeNotifier.Object,
            _mockCounterService.Object);
    }

    #region ToggleReactionAsync Tests

    [Fact]
    public async Task ToggleReactionAsync_WithNoExistingReaction_AddsReaction()
    {
        // Arrange
        var postId = PostId.New();
        var userId = UserId.New();
        var discussionId = DiscussionId.New();
        var type = ReactionType.Heart;

        var post = Post.Create(discussionId, UserId.New(), "Test content");
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
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue(); // Returns true when reaction is added

        _mockReactionRepository.Verify(r => r.AddAsync(It.IsAny<Reaction>()), Times.Once);
        _mockReactionRepository.Verify(r => r.DeleteAsync(It.IsAny<Reaction>()), Times.Never);
        _mockCounterService.Verify(c => c.IncrementUniqueReactorCountAsync(discussionId, userId), Times.Once);
        _mockRealtimeNotifier.Verify(n => n.NotifyReactionUpdatedAsync(postId, discussionId, reactionCounts), Times.Once);
    }

    [Fact]
    public async Task ToggleReactionAsync_WithSameExistingReaction_RemovesReaction()
    {
        // Arrange
        var postId = PostId.New();
        var userId = UserId.New();
        var discussionId = DiscussionId.New();
        var type = ReactionType.ThumbsUp;

        var post = Post.Create(discussionId, UserId.New(), "Test content");
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
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse(); // Returns false when reaction is removed

        _mockReactionRepository.Verify(r => r.DeleteAsync(existingReaction), Times.Once);
        _mockReactionRepository.Verify(r => r.AddAsync(It.IsAny<Reaction>()), Times.Never);
        _mockCounterService.Verify(c => c.DecrementUniqueReactorCountAsync(discussionId, userId), Times.Once);
        _mockRealtimeNotifier.Verify(n => n.NotifyReactionUpdatedAsync(postId, discussionId, reactionCounts), Times.Once);
    }

    [Fact]
    public async Task ToggleReactionAsync_WithDifferentExistingReaction_ChangesReaction()
    {
        // Arrange
        var postId = PostId.New();
        var userId = UserId.New();
        var discussionId = DiscussionId.New();
        var oldType = ReactionType.ThumbsUp;
        var newType = ReactionType.Heart;

        var post = Post.Create(discussionId, UserId.New(), "Test content");
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
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue(); // Returns true when new reaction is added

        _mockReactionRepository.Verify(r => r.DeleteAsync(existingReaction), Times.Once); // Remove old
        _mockReactionRepository.Verify(r => r.AddAsync(It.IsAny<Reaction>()), Times.Once); // Add new
        _mockCounterService.Verify(c => c.IncrementUniqueReactorCountAsync(discussionId, userId), Times.Once);
        _mockRealtimeNotifier.Verify(n => n.NotifyReactionUpdatedAsync(postId, discussionId, reactionCounts), Times.Once);
    }

    [Fact]
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
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Post not found");
        _mockReactionRepository.Verify(r => r.AddAsync(It.IsAny<Reaction>()), Times.Never);
        _mockReactionRepository.Verify(r => r.DeleteAsync(It.IsAny<Reaction>()), Times.Never);
    }

    [Fact]
    public async Task ToggleReactionAsync_AllReactionTypes_AreSupported()
    {
        // Arrange
        var postId = PostId.New();
        var userId = UserId.New();
        var discussionId = DiscussionId.New();
        var post = Post.Create(discussionId, UserId.New(), "Test content");

        _mockPostRepository.Setup(r => r.GetByPublicIdAsync(postId))
            .ReturnsAsync(post);
        _mockReactionRepository.Setup(r => r.GetByUserAndPostAsync(userId, postId))
            .ReturnsAsync((Reaction?)null);
        _mockReactionRepository.Setup(r => r.GetCountsByPostIdAsync(postId))
            .ReturnsAsync(new Dictionary<ReactionType, int>());

        // Act & Assert - All reaction types should work
        var thumbsUpResult = await _useCase.ToggleReactionAsync(postId, userId, ReactionType.ThumbsUp);
        thumbsUpResult.IsSuccess.Should().BeTrue();

        var heartResult = await _useCase.ToggleReactionAsync(postId, userId, ReactionType.Heart);
        heartResult.IsSuccess.Should().BeTrue();

        var eyesResult = await _useCase.ToggleReactionAsync(postId, userId, ReactionType.Eyes);
        eyesResult.IsSuccess.Should().BeTrue();

        _mockReactionRepository.Verify(r => r.AddAsync(It.IsAny<Reaction>()), Times.Exactly(3));
    }

    #endregion

    #region GetReactionCountsAsync Tests

    [Fact]
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
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        result[ReactionType.ThumbsUp].Should().Be(5);
        result[ReactionType.Heart].Should().Be(3);
        result[ReactionType.Eyes].Should().Be(1);
    }

    #endregion

    #region GetUserReactionAsync Tests

    [Fact]
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
        result.Should().Be(type);
    }

    [Fact]
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
        result.Should().BeNull();
    }

    #endregion

    #region Batch Methods Tests

    [Fact]
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
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        result[postIds[0].Value][ReactionType.ThumbsUp].Should().Be(3);
        result[postIds[1].Value][ReactionType.Heart].Should().Be(5);
        result[postIds[2].Value].Should().BeEmpty();
    }

    [Fact]
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
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result[postIds[0].Value].Should().Be(ReactionType.ThumbsUp);
        result[postIds[2].Value].Should().Be(ReactionType.Eyes);
        result.Should().NotContainKey(postIds[1].Value);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task ToggleReactionAsync_ToggleTwice_AddsAndRemoves()
    {
        // Arrange
        var postId = PostId.New();
        var userId = UserId.New();
        var discussionId = DiscussionId.New();
        var type = ReactionType.Heart;
        var post = Post.Create(discussionId, UserId.New(), "Test content");

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
        firstResult.IsSuccess.Should().BeTrue();
        firstResult.Value.Should().BeTrue(); // Added

        // Arrange - Second call - now there's an existing reaction
        var addedReaction = Reaction.Create(postId, userId, type);
        _mockReactionRepository.Setup(r => r.GetByUserAndPostAsync(userId, postId))
            .ReturnsAsync(addedReaction);

        // Act - Second toggle (remove)
        var secondResult = await _useCase.ToggleReactionAsync(postId, userId, type);

        // Assert second toggle
        secondResult.IsSuccess.Should().BeTrue();
        secondResult.Value.Should().BeFalse(); // Removed

        _mockReactionRepository.Verify(r => r.AddAsync(It.IsAny<Reaction>()), Times.Once);
        _mockReactionRepository.Verify(r => r.DeleteAsync(It.IsAny<Reaction>()), Times.Once);
    }

    [Fact]
    public async Task ToggleReactionAsync_ChangingReactionType_DeletesOldAndAddsNew()
    {
        // Arrange
        var postId = PostId.New();
        var userId = UserId.New();
        var discussionId = DiscussionId.New();
        var post = Post.Create(discussionId, UserId.New(), "Test content");

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
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue(); // Returns true (reaction added)

        _mockReactionRepository.Verify(r => r.DeleteAsync(thumbsUpReaction), Times.Once);
        _mockReactionRepository.Verify(r => r.AddAsync(It.Is<Reaction>(r => r.Type == ReactionType.Heart)), Times.Once);
    }

    #endregion
}
