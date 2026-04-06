namespace Snakk.Infrastructure.Database.Repositories;

using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;

public class MentionDatabaseRepository(SnakkDbContext context)
    : GenericDatabaseRepository<PostMentionDatabaseEntity>(context), IMentionDatabaseRepository
{
    public async Task<IEnumerable<PostMentionDatabaseEntity>> GetByPostIdAsync(int postId) => await _dbSet
        .Where(m => m.PostId == postId)
        .ToListAsync();

    public async Task AddRangeAsync(IEnumerable<PostMentionDatabaseEntity> mentions) =>
        await _dbSet.AddRangeAsync(mentions);

    public async Task DeleteByPostIdAsync(int postId) => await _dbSet
        .Where(m => m.PostId == postId)
        .ExecuteDeleteAsync();
}
