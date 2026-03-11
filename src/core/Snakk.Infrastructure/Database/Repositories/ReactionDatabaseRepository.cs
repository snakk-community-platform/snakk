namespace Snakk.Infrastructure.Database.Repositories;

using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;

public class ReactionDatabaseRepository(SnakkDbContext context)
    : GenericDatabaseRepository<ReactionDatabaseEntity>(context), IReactionDatabaseRepository
{
    public async Task<ReactionDatabaseEntity?> GetByUserPostAndTypeAsync(int userId, int postId, int typeId) =>
        await _dbSet.FirstOrDefaultAsync(r =>
            r.UserId == userId
            && r.PostId == postId
            && r.TypeId == typeId);

    public async Task<ReactionDatabaseEntity?> GetByUserAndPostAsync(int userId, int postId) =>
        await _dbSet.FirstOrDefaultAsync(r =>
            r.UserId == userId
            && r.PostId == postId);

    public async Task<IEnumerable<ReactionDatabaseEntity>> GetByPostIdAsync(int postId) =>
        await _dbSet
            .Where(r => r.PostId == postId)
            .ToListAsync();

    public async Task<Dictionary<int, int>> GetCountsByPostIdAsync(int postId) =>
        await _dbSet
            .Where(r => r.PostId == postId)
            .GroupBy(r => r.TypeId)
            .Select(g => new {
                Type = g.Key,
                Count = g.Count() })
            .ToDictionaryAsync(x => x.Type, x => x.Count);

    public async Task<List<int>> GetUserReactionTypesForPostAsync(int userId, int postId) =>
        await _dbSet
            .Where(r => r.UserId == userId && r.PostId == postId)
            .Select(r => r.TypeId)
            .ToListAsync();
}
