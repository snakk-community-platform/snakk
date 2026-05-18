namespace Snakk.Infrastructure.Database.Repositories;

using Snakk.Infrastructure.Database.Entities;

public interface IMentionDatabaseRepository : IGenericDatabaseRepository<PostMentionDatabaseEntity>
{
    Task<IEnumerable<PostMentionDatabaseEntity>> GetByPostIdAsync(int postId, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<PostMentionDatabaseEntity> mentions, CancellationToken ct = default);
    Task DeleteByPostIdAsync(int postId, CancellationToken ct = default);
}
