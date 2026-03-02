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
    /// Toggle a reaction on a post. If the user already has the same reaction, remove it.
    /// If the user has a different reaction, update it. If no reaction, add it.
    /// </summary>
    /// <returns>True if reaction was added, false if removed</returns>
    public async Task<Result<bool>> ToggleReactionAsync(PostId postId, UserId userId, ReactionType type)
    {
        var post = await postRepository.GetByPublicIdAsync(postId);

        if (post is null)
            return Result<bool>.Failure("Post not found");

        var existingReaction = await reactionRepository.GetByUserAndPostAsync(userId, postId);

        if (existingReaction is not null)
        {
            if (existingReaction.Type == type)
            {
                // Same reaction - remove it
                existingReaction.MarkForRemoval();
                await reactionRepository.DeleteAsync(existingReaction);

                // Update denormalized unique reactor count
                await counterService.DecrementUniqueReactorCountAsync(post.DiscussionId, userId);

                // Notify real-time update
                var counts = await reactionRepository.GetCountsByPostIdAsync(postId);
                await realtimeNotifier.NotifyReactionUpdatedAsync(postId, post.DiscussionId, counts);

                return Result<bool>.Success(false);
            }
            else
            {
                // Different reaction - remove old and add new
                existingReaction.MarkForRemoval();
                await reactionRepository.DeleteAsync(existingReaction);
            }
        }

        // Add new reaction
        var reaction = Reaction.Create(postId, userId, type);
        await reactionRepository.AddAsync(reaction);

        // Update denormalized unique reactor count
        await counterService.IncrementUniqueReactorCountAsync(post.DiscussionId, userId);

        // Notify real-time update
        var updatedCounts = await reactionRepository.GetCountsByPostIdAsync(postId);
        await realtimeNotifier.NotifyReactionUpdatedAsync(postId, post.DiscussionId, updatedCounts);

        return Result<bool>.Success(true);
    }

    public async Task<Dictionary<ReactionType, int>> GetReactionCountsAsync(PostId postId) =>
        await reactionRepository.GetCountsByPostIdAsync(postId);

    public async Task<ReactionType?> GetUserReactionAsync(PostId postId, UserId userId) =>
        await reactionRepository.GetUserReactionForPostAsync(userId, postId);

    // Batch methods for efficient loading
    public async Task<Dictionary<string, Dictionary<ReactionType, int>>> GetReactionCountsBatchAsync(IEnumerable<PostId> postIds) =>
        await reactionRepository.GetCountsByPostIdsAsync(postIds);

    public async Task<Dictionary<string, ReactionType>> GetUserReactionsBatchAsync(UserId userId, IEnumerable<PostId> postIds) =>
        await reactionRepository.GetUserReactionsForPostsAsync(userId, postIds);
}
