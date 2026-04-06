namespace Snakk.Infrastructure.Database.Repositories;

using Snakk.Infrastructure.Database.Entities;

public interface IReactionDatabaseRepository : IGenericDatabaseRepository<PostReactionDatabaseEntity>
{
    Task<PostReactionDatabaseEntity?> GetByUserPostAndTypeAsync(int userId, int postId, int typeId);
    Task<PostReactionDatabaseEntity?> GetByUserAndPostAsync(int userId, int postId);
    Task<IEnumerable<PostReactionDatabaseEntity>> GetByPostIdAsync(int postId);
    Task<Dictionary<int, int>> GetCountsByPostIdAsync(int postId);
    Task<List<int>> GetUserReactionTypesForPostAsync(int userId, int postId);
}
