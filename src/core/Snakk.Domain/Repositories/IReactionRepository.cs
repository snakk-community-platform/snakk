namespace Snakk.Domain.Repositories;

using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;

public record ReactionBatchData(
    Dictionary<string, Dictionary<ReactionType, int>> Counts,
    Dictionary<string, List<ReactionType>> UserReactions);

public interface IReactionRepository
{
    Task<Reaction?> GetByUserPostAndTypeAsync(UserId userId, PostId postId, ReactionType type);
    Task<Reaction?> GetByUserAndPostAsync(UserId userId, PostId postId);
    Task<IEnumerable<Reaction>> GetByPostIdAsync(PostId postId);
    Task<Dictionary<ReactionType, int>> GetCountsByPostIdAsync(PostId postId);
    Task<List<ReactionType>> GetUserReactionsForPostAsync(UserId userId, PostId postId);
    Task AddAsync(Reaction reaction);
    Task DeleteAsync(Reaction reaction);

    // Batch methods for efficient loading
    Task<Dictionary<string, Dictionary<ReactionType, int>>> GetCountsByPostIdsAsync(IEnumerable<PostId> postIds);
    Task<Dictionary<string, List<ReactionType>>> GetUserReactionsForPostsAsync(UserId userId, IEnumerable<PostId> postIds);
    Task<ReactionBatchData> GetReactionDataAsync(IEnumerable<PostId> postIds, UserId? userId);

    // Aggregate query — total reactions on all posts authored by this user.
    Task<int> GetTotalReactionsReceivedByUserAsync(UserId userId);
}
