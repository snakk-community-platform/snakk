namespace Snakk.Infrastructure.Database.Repositories;

using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;

public class ReactionDatabaseRepository(SnakkDbContext context)
    : GenericDatabaseRepository<ReactionDatabaseEntity>(context), IReactionDatabaseRepository
{
    public override async Task<ReactionDatabaseEntity?> GetByIdAsync(int id)
    {
        return await _dbSet
            .Include(r => r.Post)
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public override async Task<IEnumerable<ReactionDatabaseEntity>> GetAllAsync()
    {
        return await _dbSet
            .Include(r => r.Post)
            .Include(r => r.User)
            .ToListAsync();
    }

    public async Task<ReactionDatabaseEntity?> GetByUserAndPostAsync(int userId, int postId)
    {
        return await _dbSet
            .Include(r => r.Post)
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.UserId == userId && r.PostId == postId);
    }

    public async Task<IEnumerable<ReactionDatabaseEntity>> GetByPostIdAsync(int postId)
    {
        return await _dbSet
            .AsNoTracking()
            .Include(r => r.Post)
            .Include(r => r.User)
            .Where(r => r.PostId == postId)
            .ToListAsync();
    }

    public async Task<Dictionary<int, int>> GetCountsByPostIdAsync(int postId)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(r => r.PostId == postId)
            .GroupBy(r => r.TypeId)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Type, x => x.Count);
    }

    public async Task<int?> GetUserReactionTypeForPostAsync(int userId, int postId)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(r => r.UserId == userId && r.PostId == postId)
            .Select(r => r.TypeId)
            .FirstOrDefaultAsync();
    }
}
