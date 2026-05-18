namespace Snakk.Infrastructure.Database;

using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database;

public class GenericDatabaseRepository<T>(SnakkDbContext context) : IGenericDatabaseRepository<T> where T : class
{
    protected readonly SnakkDbContext _context = context;
    protected readonly DbSet<T> _dbSet = context.Set<T>();

    public virtual async Task<T?> GetByIdAsync(int id, CancellationToken ct = default) =>
        await _dbSet.FindAsync([id], ct);

    public virtual async Task<IEnumerable<T>> GetAllAsync(CancellationToken ct = default) =>
        await _dbSet.ToListAsync(ct);

    public virtual async Task AddAsync(T entity, CancellationToken ct = default) =>
        await _dbSet.AddAsync(entity, ct);

    public virtual Task UpdateAsync(T entity, CancellationToken ct = default)
    {
        _dbSet.Update(entity);
        return Task.CompletedTask;
    }

    public virtual Task DeleteAsync(T entity, CancellationToken ct = default)
    {
        _dbSet.Remove(entity);
        return Task.CompletedTask;
    }

    public virtual async Task SaveChangesAsync(CancellationToken ct = default) =>
        await _context.SaveChangesAsync(ct);
}
