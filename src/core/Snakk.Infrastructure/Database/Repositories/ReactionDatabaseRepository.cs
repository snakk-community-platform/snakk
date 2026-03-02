namespace Snakk.Infrastructure.Database.Repositories;

using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;

public class ReactionDatabaseRepository(SnakkDbContext context)
    : GenericDatabaseRepository<ReactionDatabaseEntity>(context), IReactionDatabaseRepository
{
    private readonly SnakkDbContext _context = context;

    private static readonly Func<SnakkDbContext, int, int, Task<int?>> _getUserReactionType
        = EF.CompileAsyncQuery(
            (SnakkDbContext ctx, int userId, int postId) =>
                ctx.Reactions
                    .Where(r => r.UserId == userId && r.PostId == postId)
                    .Select(r => (int?)r.TypeId)
                    .FirstOrDefault());

    public async Task<ReactionDatabaseEntity?> GetByUserAndPostAsync(int userId, int postId)
    {
        return await _dbSet
            .FirstOrDefaultAsync(r => r.UserId == userId && r.PostId == postId);
    }

    public async Task<IEnumerable<ReactionDatabaseEntity>> GetByPostIdAsync(int postId)
    {
        return await _dbSet
            .Where(r => r.PostId == postId)
            .ToListAsync();
    }

    public async Task<Dictionary<int, int>> GetCountsByPostIdAsync(int postId)
    {
        return await _dbSet
            .Where(r => r.PostId == postId)
            .GroupBy(r => r.TypeId)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Type, x => x.Count);
    }

    public async Task<int?> GetUserReactionTypeForPostAsync(int userId, int postId)
    {
        return await _getUserReactionType(_context, userId, postId);
    }
}
