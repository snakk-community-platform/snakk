namespace Snakk.Infrastructure.Database;

using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database;

public class GenericDatabaseRepository<T>(SnakkDbContext context) : IGenericDatabaseRepository<T> where T : class
{
    protected readonly SnakkDbContext _context = context;
    protected readonly DbSet<T> _dbSet = context.Set<T>();

    public virtual async Task<T?> GetByIdAsync(int id) =>
        await _dbSet.FindAsync(id);

    public virtual async Task<IEnumerable<T>> GetAllAsync() =>
        await _dbSet.ToListAsync();

    public virtual async Task AddAsync(T entity) =>
        await _dbSet.AddAsync(entity);

    public virtual Task UpdateAsync(T entity)
    {
        _dbSet.Update(entity);
        return Task.CompletedTask;
    }

    public virtual Task DeleteAsync(T entity)
    {
        _dbSet.Remove(entity);
        return Task.CompletedTask;
    }

    public virtual async Task SaveChangesAsync() =>
        await _context.SaveChangesAsync();
}
