namespace Snakk.Domain.Repositories;

using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;

public interface IReactionRepository
{
    Task<Reaction?> GetByUserAndPostAsync(UserId userId, PostId postId);
    Task<IEnumerable<Reaction>> GetByPostIdAsync(PostId postId);
    Task<Dictionary<ReactionType, int>> GetCountsByPostIdAsync(PostId postId);
    Task<ReactionType?> GetUserReactionForPostAsync(UserId userId, PostId postId);
    Task AddAsync(Reaction reaction);
    Task DeleteAsync(Reaction reaction);

    // Batch methods for efficient loading
    Task<Dictionary<string, Dictionary<ReactionType, int>>> GetCountsByPostIdsAsync(IEnumerable<PostId> postIds);
    Task<Dictionary<string, ReactionType>> GetUserReactionsForPostsAsync(UserId userId, IEnumerable<PostId> postIds);
}
