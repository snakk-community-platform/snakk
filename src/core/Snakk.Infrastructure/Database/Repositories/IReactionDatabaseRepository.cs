namespace Snakk.Infrastructure.Database.Repositories;

using Snakk.Infrastructure.Database.Entities;

public interface IReactionDatabaseRepository : IGenericDatabaseRepository<ReactionDatabaseEntity>
{
    Task<ReactionDatabaseEntity?> GetByUserPostAndTypeAsync(int userId, int postId, int typeId);
    Task<ReactionDatabaseEntity?> GetByUserAndPostAsync(int userId, int postId);
    Task<IEnumerable<ReactionDatabaseEntity>> GetByPostIdAsync(int postId);
    Task<Dictionary<int, int>> GetCountsByPostIdAsync(int postId);
    Task<List<int>> GetUserReactionTypesForPostAsync(int userId, int postId);
}
