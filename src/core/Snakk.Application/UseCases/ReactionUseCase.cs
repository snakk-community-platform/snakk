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
    /// If the user already has the same reaction type, remove it (toggle off).
    /// If the user has a different reaction type, remove the old one and add the new one (switch).
    /// If the user has no reaction, add the new one.
    /// </summary>
    /// <returns>True if a reaction was added (new or switched), false if removed</returns>
    public async Task<Result<bool>> ToggleReactionAsync(PostId postId, UserId userId, ReactionType type)
    {
        var post = await postRepository.GetByPublicIdAsync(postId);

        if (post is null)
            return Result<bool>.Failure("Post not found");

        var existingReaction = await reactionRepository.GetByUserAndPostAsync(userId, postId);

        if (existingReaction is not null)
        {
            // Remove the existing reaction
            existingReaction.MarkForRemoval();
            await reactionRepository.DeleteAsync(existingReaction);
            await counterService.DecrementReactionCountAsync(postId, post.DiscussionId);

            if (existingReaction.Type == type)
            {
                // Same type — toggle off, we're done
                var counts = await reactionRepository.GetCountsByPostIdAsync(postId);
                await realtimeNotifier.NotifyReactionUpdatedAsync(postId, post.DiscussionId, counts);
                return Result<bool>.Success(false);
            }
        }

        // Add the new reaction (either first reaction or switching type)
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
