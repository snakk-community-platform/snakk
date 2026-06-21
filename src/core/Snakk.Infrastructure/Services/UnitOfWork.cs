namespace Snakk.Infrastructure.Services;

using Snakk.Application.Services;
using Snakk.Infrastructure.Database;

public class UnitOfWork(SnakkDbContext db) : IUnitOfWork
{
    public async Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            await operation();
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }
}
