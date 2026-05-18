namespace Snakk.Infrastructure.Database.Repositories;

using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;

public class MentionDatabaseRepository(SnakkDbContext context)
    : GenericDatabaseRepository<PostMentionDatabaseEntity>(context), IMentionDatabaseRepository
{
    public async Task<IEnumerable<PostMentionDatabaseEntity>> GetByPostIdAsync(int postId, CancellationToken ct = default) => await _dbSet
        .Where(m => m.PostId == postId)
        .ToListAsync(ct);

    public async Task AddRangeAsync(IEnumerable<PostMentionDatabaseEntity> mentions, CancellationToken ct = default) =>
        await _dbSet.AddRangeAsync(mentions, ct);

    public async Task DeleteByPostIdAsync(int postId, CancellationToken ct = default) => await _dbSet
        .Where(m => m.PostId == postId)
        .ExecuteDeleteAsync(ct);
}
