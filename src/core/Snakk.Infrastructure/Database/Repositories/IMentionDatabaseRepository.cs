namespace Snakk.Infrastructure.Database.Repositories;

using Snakk.Infrastructure.Database.Entities;

public interface IMentionDatabaseRepository : IGenericDatabaseRepository<PostMentionDatabaseEntity>
{
    Task<IEnumerable<PostMentionDatabaseEntity>> GetByPostIdAsync(int postId);
    Task AddRangeAsync(IEnumerable<PostMentionDatabaseEntity> mentions);
    Task DeleteByPostIdAsync(int postId);
}
