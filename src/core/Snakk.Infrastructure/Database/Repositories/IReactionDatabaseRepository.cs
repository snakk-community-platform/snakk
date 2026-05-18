namespace Snakk.Infrastructure.Database.Repositories;

using Snakk.Infrastructure.Database.Entities;

public interface IReactionDatabaseRepository : IGenericDatabaseRepository<PostReactionDatabaseEntity>
{
    Task<PostReactionDatabaseEntity?> GetByUserPostAndTypeAsync(int userId, int postId, int typeId, CancellationToken ct = default);
    Task<PostReactionDatabaseEntity?> GetByUserAndPostAsync(int userId, int postId, CancellationToken ct = default);
    Task<IEnumerable<PostReactionDatabaseEntity>> GetByPostIdAsync(int postId, CancellationToken ct = default);
    Task<Dictionary<int, int>> GetCountsByPostIdAsync(int postId, CancellationToken ct = default);
    Task<List<int>> GetUserReactionTypesForPostAsync(int userId, int postId, CancellationToken ct = default);
}
