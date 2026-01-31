namespace Snakk.Infrastructure.Database.Repositories;

using Snakk.Infrastructure.Database.Entities;

public interface IReactionDatabaseRepository : IGenericDatabaseRepository<ReactionDatabaseEntity>
{
    Task<ReactionDatabaseEntity?> GetByUserAndPostAsync(int userId, int postId);
    Task<IEnumerable<ReactionDatabaseEntity>> GetByPostIdAsync(int postId);
    Task<Dictionary<int, int>> GetCountsByPostIdAsync(int postId);
    Task<int?> GetUserReactionTypeForPostAsync(int userId, int postId);
}
