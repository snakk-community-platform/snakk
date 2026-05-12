using NSubstitute;
using Snakk.Application.Services;
using Snakk.Application.UseCases;
using Snakk.Domain.Entities;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;

namespace Snakk.Application.Tests.UseCases;

public class ReactionUseCaseTests
{
    private readonly IReactionRepository _reactionRepository = Substitute.For<IReactionRepository>();
    private readonly IPostRepository _postRepository = Substitute.For<IPostRepository>();
    private readonly IDiscussionRepository _discussionRepository = Substitute.For<IDiscussionRepository>();
    private readonly IRealtimeNotifier _realtimeNotifier = Substitute.For<IRealtimeNotifier>();
    private readonly ICounterService _counterService = Substitute.For<ICounterService>();
    private ReactionUseCase _useCase = null!;

    [Before(Test)]
    public void Setup()
    {
        _useCase = new ReactionUseCase(_reactionRepository, _postRepository, _discussionRepository, _realtimeNotifier, _counterService);
    }

    #region ToggleReactionAsync Tests

    [Test]
    public async Task ToggleReactionAsync_WithNoExistingReaction_AddsReaction()
    {
        var postId = PostId.New();
        var userId = UserId.New();
        var discussionId = DiscussionId.New();
        var type = ReactionType.Love;
        var post = Post.Create(discussionId, UserId.New(), "Test content", "<p>Test content</p>");
        var reactionCounts = new Dictionary<ReactionType, int> { { ReactionType.Love, 1 } };

        _postRepository.GetByPublicIdAsync(postId).Returns(post);
        _reactionRepository.GetByUserAndPostAsync(userId, postId).Returns((Reaction?)null);
        _reactionRepository.GetCountsByPostIdAsync(postId).Returns(reactionCounts);

        var result = await _useCase.ToggleReactionAsync(postId, userId, type);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsTrue();
        await _reactionRepository.Received(1).AddAsync(Arg.Any<Reaction>());
        await _reactionRepository.DidNotReceive().DeleteAsync(Arg.Any<Reaction>());
        await _counterService.Received(1).IncrementReactionCountAsync(postId, discussionId);
        await _realtimeNotifier.Received(1).NotifyReactionUpdatedAsync(postId, discussionId, reactionCounts);
    }

    [Test]
    public async Task ToggleReactionAsync_WithExistingReaction_RemovesReaction()
    {
        var postId = PostId.New();
        var userId = UserId.New();
        var discussionId = DiscussionId.New();
        var type = ReactionType.Agree;
        var post = Post.Create(discussionId, UserId.New(), "Test content", "<p>Test content</p>");
        var existingReaction = Reaction.Create(postId, userId, type);
        var reactionCounts = new Dictionary<ReactionType, int>();

        _postRepository.GetByPublicIdAsync(postId).Returns(post);
        _reactionRepository.GetByUserAndPostAsync(userId, postId).Returns(existingReaction);
        _reactionRepository.GetCountsByPostIdAsync(postId).Returns(reactionCounts);

        var result = await _useCase.ToggleReactionAsync(postId, userId, type);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsFalse();
        await _reactionRepository.Received(1).DeleteAsync(existingReaction);
        await _reactionRepository.DidNotReceive().AddAsync(Arg.Any<Reaction>());
        await _counterService.Received(1).DecrementReactionCountAsync(postId, discussionId);
        await _realtimeNotifier.Received(1).NotifyReactionUpdatedAsync(postId, discussionId, reactionCounts);
    }

    [Test]
    public async Task ToggleReactionAsync_AddingDifferentType_ReplacesExisting()
    {
        var postId = PostId.New();
        var userId = UserId.New();
        var discussionId = DiscussionId.New();
        var existingType = ReactionType.Agree;
        var newType = ReactionType.Love;
        var post = Post.Create(discussionId, UserId.New(), "Test content", "<p>Test content</p>");
        var existingReaction = Reaction.Create(postId, userId, existingType);
        var reactionCounts = new Dictionary<ReactionType, int> { { ReactionType.Love, 1 } };

        _postRepository.GetByPublicIdAsync(postId).Returns(post);
        _reactionRepository.GetByUserAndPostAsync(userId, postId).Returns(existingReaction);
        _reactionRepository.GetCountsByPostIdAsync(postId).Returns(reactionCounts);

        var result = await _useCase.ToggleReactionAsync(postId, userId, newType);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsTrue();
        await _reactionRepository.Received(1).DeleteAsync(existingReaction);
        await _reactionRepository.Received(1).AddAsync(Arg.Any<Reaction>());
        await _counterService.DidNotReceive().IncrementReactionCountAsync(postId, discussionId);
        await _counterService.DidNotReceive().DecrementReactionCountAsync(postId, discussionId);
        await _realtimeNotifier.Received(1).NotifyReactionUpdatedAsync(postId, discussionId, reactionCounts);
    }

    [Test]
    public async Task ToggleReactionAsync_WithNonExistentPost_ReturnsFailure()
    {
        var postId = PostId.New();
        var userId = UserId.New();
        _postRepository.GetByPublicIdAsync(postId).Returns((Post?)null);

        var result = await _useCase.ToggleReactionAsync(postId, userId, ReactionType.Love);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Post not found");
        await _reactionRepository.DidNotReceive().AddAsync(Arg.Any<Reaction>());
        await _reactionRepository.DidNotReceive().DeleteAsync(Arg.Any<Reaction>());
    }

    [Test]
    public async Task ToggleReactionAsync_AllReactionTypes_AreSupported()
    {
        var postId = PostId.New();
        var userId = UserId.New();
        var discussionId = DiscussionId.New();
        var post = Post.Create(discussionId, UserId.New(), "Test content", "<p>Test content</p>");

        _postRepository.GetByPublicIdAsync(postId).Returns(post);
        _reactionRepository.GetByUserAndPostAsync(userId, postId).Returns((Reaction?)null);
        _reactionRepository.GetCountsByPostIdAsync(postId).Returns(new Dictionary<ReactionType, int>());

        foreach (var type in Enum.GetValues<ReactionType>())
        {
            var result = await _useCase.ToggleReactionAsync(postId, userId, type);
            await Assert.That(result.IsSuccess).IsTrue();
        }

        await _reactionRepository.Received(9).AddAsync(Arg.Any<Reaction>());
    }

    #endregion

    #region GetReactionCountsAsync Tests

    [Test]
    public async Task GetReactionCountsAsync_ReturnsCounts()
    {
        var postId = PostId.New();
        var counts = new Dictionary<ReactionType, int> { { ReactionType.Agree, 5 }, { ReactionType.Love, 3 }, { ReactionType.Watching, 1 } };
        _reactionRepository.GetCountsByPostIdAsync(postId).Returns(counts);

        var result = await _useCase.GetReactionCountsAsync(postId);

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
        var postId = PostId.New();
        var userId = UserId.New();
        var types = new List<ReactionType> { ReactionType.Love, ReactionType.Fire };
        _reactionRepository.GetUserReactionsForPostAsync(userId, postId).Returns(types);

        var result = await _useCase.GetUserReactionsAsync(postId, userId);

        await Assert.That(result).Count().IsEqualTo(2);
        await Assert.That(result).Contains(ReactionType.Love);
        await Assert.That(result).Contains(ReactionType.Fire);
    }

    [Test]
    public async Task GetUserReactionsAsync_WithNoReactions_ReturnsEmptyList()
    {
        var postId = PostId.New();
        var userId = UserId.New();
        _reactionRepository.GetUserReactionsForPostAsync(userId, postId).Returns(new List<ReactionType>());

        var result = await _useCase.GetUserReactionsAsync(postId, userId);

        await Assert.That(result).IsEmpty();
    }

    #endregion

    #region Batch Methods Tests

    [Test]
    public async Task GetReactionCountsBatchAsync_ReturnsCountsForMultiplePosts()
    {
        var postIds = new List<PostId> { PostId.New(), PostId.New(), PostId.New() };
        var batchCounts = new Dictionary<string, Dictionary<ReactionType, int>>
        {
            { postIds[0].Value, new Dictionary<ReactionType, int> { { ReactionType.Agree, 3 } } },
            { postIds[1].Value, new Dictionary<ReactionType, int> { { ReactionType.Love, 5 } } },
            { postIds[2].Value, new Dictionary<ReactionType, int>() }
        };
        _reactionRepository.GetCountsByPostIdsAsync(postIds).Returns(batchCounts);

        var result = await _useCase.GetReactionCountsBatchAsync(postIds);

        await Assert.That(result).IsNotNull();
        await Assert.That(result).Count().IsEqualTo(3);
        await Assert.That(result[postIds[0].Value][ReactionType.Agree]).IsEqualTo(3);
        await Assert.That(result[postIds[1].Value][ReactionType.Love]).IsEqualTo(5);
        await Assert.That(result[postIds[2].Value]).IsEmpty();
    }

    [Test]
    public async Task GetUserReactionsBatchAsync_ReturnsUserReactionsForMultiplePosts()
    {
        var userId = UserId.New();
        var postIds = new List<PostId> { PostId.New(), PostId.New(), PostId.New() };
        var userReactions = new Dictionary<string, List<ReactionType>>
        {
            { postIds[0].Value, new List<ReactionType> { ReactionType.Agree, ReactionType.Fire } },
            { postIds[2].Value, new List<ReactionType> { ReactionType.Watching } }
        };
        _reactionRepository.GetUserReactionsForPostsAsync(userId, postIds).Returns(userReactions);

        var result = await _useCase.GetUserReactionsBatchAsync(userId, postIds);

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
        var postId = PostId.New();
        var userId = UserId.New();
        var discussionId = DiscussionId.New();
        var type = ReactionType.Love;
        var post = Post.Create(discussionId, UserId.New(), "Test content", "<p>Test content</p>");

        _postRepository.GetByPublicIdAsync(postId).Returns(post);
        _reactionRepository.GetCountsByPostIdAsync(postId).Returns(new Dictionary<ReactionType, int>());

        // First call - no existing reaction
        _reactionRepository.GetByUserAndPostAsync(userId, postId).Returns((Reaction?)null);

        var firstResult = await _useCase.ToggleReactionAsync(postId, userId, type);

        await Assert.That(firstResult.IsSuccess).IsTrue();
        await Assert.That(firstResult.Value).IsTrue();

        // Second call - now there's an existing reaction
        var addedReaction = Reaction.Create(postId, userId, type);
        _reactionRepository.GetByUserAndPostAsync(userId, postId).Returns(addedReaction);

        var secondResult = await _useCase.ToggleReactionAsync(postId, userId, type);

        await Assert.That(secondResult.IsSuccess).IsTrue();
        await Assert.That(secondResult.Value).IsFalse();

        await _reactionRepository.Received(1).AddAsync(Arg.Any<Reaction>());
        await _reactionRepository.Received(1).DeleteAsync(Arg.Any<Reaction>());
    }

    [Test]
    public async Task ToggleReactionAsync_DifferentType_ReplacesExistingReaction()
    {
        var postId = PostId.New();
        var userId = UserId.New();
        var discussionId = DiscussionId.New();
        var post = Post.Create(discussionId, UserId.New(), "Test content", "<p>Test content</p>");

        _postRepository.GetByPublicIdAsync(postId).Returns(post);
        _reactionRepository.GetCountsByPostIdAsync(postId).Returns(new Dictionary<ReactionType, int>());

        // First call - no existing reaction
        _reactionRepository.GetByUserAndPostAsync(userId, postId).Returns((Reaction?)null);

        var agreeResult = await _useCase.ToggleReactionAsync(postId, userId, ReactionType.Agree);

        // Second call - Agree exists, Love should replace it
        var agreeReaction = Reaction.Create(postId, userId, ReactionType.Agree);
        _reactionRepository.GetByUserAndPostAsync(userId, postId).Returns(agreeReaction);

        var loveResult = await _useCase.ToggleReactionAsync(postId, userId, ReactionType.Love);

        await Assert.That(agreeResult.IsSuccess).IsTrue();
        await Assert.That(agreeResult.Value).IsTrue();
        await Assert.That(loveResult.IsSuccess).IsTrue();
        await Assert.That(loveResult.Value).IsTrue();

        // 2 adds (Agree + Love), 1 delete (Agree removed on replace)
        await _reactionRepository.Received(2).AddAsync(Arg.Any<Reaction>());
        await _reactionRepository.Received(1).DeleteAsync(Arg.Any<Reaction>());
        // Only the first add increments the counter; the replace keeps it unchanged
        await _counterService.Received(1).IncrementReactionCountAsync(postId, discussionId);
        await _counterService.DidNotReceive().DecrementReactionCountAsync(postId, discussionId);
    }

    #endregion
}
