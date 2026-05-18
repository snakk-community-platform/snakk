namespace Snakk.Domain.Repositories;

using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;

public record ReactionBatchData(
    Dictionary<string, Dictionary<ReactionType, int>> Counts,
    Dictionary<string, List<ReactionType>> UserReactions);

public interface IReactionRepository
{
    Task<Reaction?> GetByUserPostAndTypeAsync(UserId userId, PostId postId, ReactionType type, CancellationToken ct = default);
    Task<Reaction?> GetByUserAndPostAsync(UserId userId, PostId postId, CancellationToken ct = default);
    Task<IEnumerable<Reaction>> GetByPostIdAsync(PostId postId, CancellationToken ct = default);
    Task<Dictionary<ReactionType, int>> GetCountsByPostIdAsync(PostId postId, CancellationToken ct = default);
    Task<List<ReactionType>> GetUserReactionsForPostAsync(UserId userId, PostId postId, CancellationToken ct = default);
    Task AddAsync(Reaction reaction, CancellationToken ct = default);
    Task DeleteAsync(Reaction reaction, CancellationToken ct = default);

    // Batch methods for efficient loading
    Task<Dictionary<string, Dictionary<ReactionType, int>>> GetCountsByPostIdsAsync(IEnumerable<PostId> postIds, CancellationToken ct = default);
    Task<Dictionary<string, List<ReactionType>>> GetUserReactionsForPostsAsync(UserId userId, IEnumerable<PostId> postIds, CancellationToken ct = default);
    Task<ReactionBatchData> GetReactionDataAsync(IEnumerable<PostId> postIds, UserId? userId, CancellationToken ct = default);

    // Aggregate query — total reactions on all posts authored by this user.
    Task<int> GetTotalReactionsReceivedByUserAsync(UserId userId, CancellationToken ct = default);
}
