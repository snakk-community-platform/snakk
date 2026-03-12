namespace Snakk.Application.UseCases;

using Snakk.Domain.Entities;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;
using Snakk.Application.Services;
using Snakk.Shared.Models;

public class ReactionUseCase(
    IReactionRepository reactionRepository,
    IPostRepository postRepository,
    IRealtimeNotifier realtimeNotifier,
    ICounterService counterService)
{
    /// <summary>
    /// Toggle a reaction on a post. Each user may have at most one reaction per post.
    /// - Same type as existing → remove (toggle off), decrement counter
    /// - Different type → replace (delete old, add new), counter unchanged
    /// - No existing reaction → add, increment counter
    /// </summary>
    /// <returns>True if a reaction is now active, false if removed</returns>
    public async Task<Result<bool>> ToggleReactionAsync(PostId postId, UserId userId, ReactionType type)
    {
        var post = await postRepository.GetByPublicIdAsync(postId);

        if (post is null)
            return Result<bool>.Failure("Post not found");

        var existingReaction = await reactionRepository.GetByUserAndPostAsync(userId, postId);

        if (existingReaction is not null)
        {
            existingReaction.MarkForRemoval();
            await reactionRepository.DeleteAsync(existingReaction);

            if (existingReaction.Type == type)
            {
                // Toggle off — same type clicked again
                await counterService.DecrementReactionCountAsync(postId, post.DiscussionId);
                var counts = await reactionRepository.GetCountsByPostIdAsync(postId);
                await realtimeNotifier.NotifyReactionUpdatedAsync(postId, post.DiscussionId, counts);
                return Result<bool>.Success(false);
            }

            // Replace with new type — counter stays the same
            var replacement = Reaction.Create(postId, userId, type);
            await reactionRepository.AddAsync(replacement);
            var replacedCounts = await reactionRepository.GetCountsByPostIdAsync(postId);
            await realtimeNotifier.NotifyReactionUpdatedAsync(postId, post.DiscussionId, replacedCounts);
            return Result<bool>.Success(true);
        }

        var reaction = Reaction.Create(postId, userId, type);
        await reactionRepository.AddAsync(reaction);
        await counterService.IncrementReactionCountAsync(postId, post.DiscussionId);

        var updatedCounts = await reactionRepository.GetCountsByPostIdAsync(postId);
        await realtimeNotifier.NotifyReactionUpdatedAsync(postId, post.DiscussionId, updatedCounts);

        return Result<bool>.Success(true);
    }

    public async Task<Dictionary<ReactionType, int>> GetReactionCountsAsync(PostId postId) =>
        await reactionRepository.GetCountsByPostIdAsync(postId);

    public async Task<List<ReactionType>> GetUserReactionsAsync(PostId postId, UserId userId) =>
        await reactionRepository.GetUserReactionsForPostAsync(userId, postId);

    // Batch methods for efficient loading
    public async Task<Dictionary<string, Dictionary<ReactionType, int>>> GetReactionCountsBatchAsync(IEnumerable<PostId> postIds) =>
        await reactionRepository.GetCountsByPostIdsAsync(postIds);

    public async Task<Dictionary<string, List<ReactionType>>> GetUserReactionsBatchAsync(UserId userId, IEnumerable<PostId> postIds) =>
        await reactionRepository.GetUserReactionsForPostsAsync(userId, postIds);
}
