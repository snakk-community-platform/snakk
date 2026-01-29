namespace Snakk.Infrastructure.Database.Repositories;

using Snakk.Infrastructure.Database.Entities;

public interface IMentionDatabaseRepository : IGenericDatabaseRepository<MentionDatabaseEntity>
{
    Task<IEnumerable<MentionDatabaseEntity>> GetByPostIdAsync(int postId);
    Task AddRangeAsync(IEnumerable<MentionDatabaseEntity> mentions);
    Task DeleteByPostIdAsync(int postId);
}
