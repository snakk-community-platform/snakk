namespace Snakk.Infrastructure.Database.Repositories;

using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;

public class MentionDatabaseRepository(SnakkDbContext context)
    : GenericDatabaseRepository<MentionDatabaseEntity>(context), IMentionDatabaseRepository
{
    public override async Task<MentionDatabaseEntity?> GetByIdAsync(int id)
    {
        return await _dbSet
            .Include(m => m.Post)
            .Include(m => m.MentionedUser)
            .FirstOrDefaultAsync(m => m.Id == id);
    }

    public override async Task<IEnumerable<MentionDatabaseEntity>> GetAllAsync()
    {
        return await _dbSet
            .Include(m => m.Post)
            .Include(m => m.MentionedUser)
            .ToListAsync();
    }

    public async Task<IEnumerable<MentionDatabaseEntity>> GetByPostIdAsync(int postId)
    {
        return await _dbSet
            .AsNoTracking()
            .Include(m => m.Post)
            .Include(m => m.MentionedUser)
            .Where(m => m.PostId == postId)
            .ToListAsync();
    }

    public async Task AddRangeAsync(IEnumerable<MentionDatabaseEntity> mentions)
    {
        await _dbSet.AddRangeAsync(mentions);
    }

    public async Task DeleteByPostIdAsync(int postId)
    {
        await _dbSet
            .Where(m => m.PostId == postId)
            .ExecuteDeleteAsync();
    }
}
